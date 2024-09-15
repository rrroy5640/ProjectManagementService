using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using ProjectManagementService.DTOs.Requests;
using ProjectManagementService.DTOs.Responses;
using ProjectManagementService.Models;
using ProjectManagementService.Services;

namespace ProjectManagementService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly ProjectService _projectService;

        public ProjectController(ProjectService projectService)
        {
            _projectService = projectService;
        }

        private bool ValidateCurrentUser(string userId)
        {
            var currentUserId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return currentUserId == userId;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<List<ProjectDTO>>> GetAllProjects()
        {
            try
            {
                var projects = await _projectService.GetAllProjectsAsync();
                var projectDTOs = projects.Select(project => new ProjectDTO
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    ProjectMembers = project.ProjectMembers,
                    ProjectOwner = project.ProjectOwner,
                    TaskIds = project.TaskIds,
                    Status = project.Status.ToString()
                }).ToList();

                return projectDTOs;
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ProjectDTO>> CreateProject(CreateProjectRequest project)
        {
            try
            {
                // Initialize a list to store the task IDs
                var taskIds = new List<string>();

                // Check if tasks are provided in the request
                if (project.Tasks != null && project.Tasks.Count > 0)
                {
                    // Create each task and collect its ID
                    foreach (var taskRequest in project.Tasks)
                    {
                        if (!Enum.TryParse<ProjectTaskStatus>(taskRequest.Status, true, out var taskStatus))
                        {
                            return BadRequest("Invalid task status value.");
                        }

                        var newTask = new ProjectTask
                        {
                            Title = taskRequest.Title,
                            Description = taskRequest.Description,
                            AssignedTo = taskRequest.AssignedTo,
                            DueDate = taskRequest.DueDate,
                            Status = taskStatus
                        };

                        // Create the task in the database
                        var taskId = await _projectService.CreateTask(newTask);

                        // Check if the task was created successfully
                        if (taskId == null)
                        {
                            return StatusCode(500, "Failed to create one of the tasks.");
                        }

                        // Add the created task ID to the list
                        taskIds.Add(taskId);
                    }
                }

                // Create the new project with the collected task IDs
                var newProject = new Project
                {
                    Name = project.Name,
                    Description = project.Description,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    ProjectMembers = project.ProjectMemberIds,
                    ProjectOwner = project.ProjectOwner,
                    TaskIds = taskIds,
                    Status = ProjectStatus.NotStarted
                };

                // Create the project in the database
                var createdProject = await _projectService.CreateProject(newProject);

                var projectDTO = new ProjectDTO
                {
                    Id = createdProject.Id,
                    Name = createdProject.Name,
                    Description = createdProject.Description,
                    StartDate = createdProject.StartDate,
                    EndDate = createdProject.EndDate,
                    ProjectMembers = createdProject.ProjectMembers,
                    ProjectOwner = createdProject.ProjectOwner,
                    TaskIds = createdProject.TaskIds,
                    Status = createdProject.Status.ToString()
                };

                // Check if the project was created successfully
                if (createdProject == null)
                {
                    return StatusCode(500, "Failed to create the project.");
                }

                // Return the created project details
                return CreatedAtRoute("GetProject", new { id = projectDTO.Id }, projectDTO);
            }
            catch (Exception e)
            {
                // Log the exception (optional) and return a 500 status code with error message
                Console.WriteLine($"Error creating project: {e.Message}");
                return StatusCode(500, e.Message);
            }
        }

        [HttpGet("{id:length(24)}", Name = "GetProject")]
        [Authorize]
        public async Task<ActionResult<ProjectDTO>> GetProjectById(string id)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID claim is missing or invalid.");
                }

                var project = await _projectService.GetProjectById(id);
                if (project == null)
                {
                    return NotFound();
                }

                if (string.IsNullOrEmpty(project.Id))
                {
                    return BadRequest("Project ID is missing or invalid.");
                }

                if (!await _projectService.UserHasAccessToProject(userId, project.Id))
                {
                    return Unauthorized();
                }

                var projectDTO = new ProjectDTO
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    ProjectMembers = project.ProjectMembers,
                    ProjectOwner = project.ProjectOwner,
                    TaskIds = project.TaskIds,
                    Status = project.Status.ToString()
                };

                return Ok(projectDTO);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPut("{id:length(24)}")]
        [Authorize]
        public async Task<ActionResult<Project>> UpdateProject(string id, UpdateProjectRequest project)
        {
            try
            {
                // Fetch the project by ID
                var projectIn = await _projectService.GetProjectById(id);

                // Check if the project exists
                if (projectIn == null)
                {
                    return NotFound($"Project with ID {id} not found.");
                }

                // Update project properties from the request
                projectIn.Name = project.Name;
                projectIn.Description = project.Description;
                projectIn.StartDate = project.StartDate;
                projectIn.EndDate = project.EndDate;
                projectIn.ProjectMembers = project.ProjectMemberIds;
                projectIn.ProjectOwner = project.ProjectOwner;

                // Update the project in the database
                var updatedProject = await _projectService.UpdateProject(id, projectIn);

                if (updatedProject == null)
                {
                    return StatusCode(500, "Failed to update the project.");
                }

                // Return the updated project
                return Ok(updatedProject);
            }
            catch (Exception e)
            {
                // Log the exception (optional)
                Console.WriteLine($"Error updating project: {e.Message}");
                return StatusCode(500, "An error occurred while updating the project.");
            }
        }


        [HttpDelete("{id:length(24)}")]
        [Authorize]
        public async Task<IActionResult> DeleteProjectById(string id)
        {
            try
            {
                var project = await _projectService.GetProjectById(id);

                if (project == null)
                {
                    return NotFound();
                }

                await _projectService.RemoveProject(id);

                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost("{id:length(24)}/members")]
        [Authorize]
        public async Task<IActionResult> AddMember(string id, string userId)
        {
            try
            {
                await _projectService.AddProjectMember(id, userId);
                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpDelete("{id:length(24)}/members")]
        [Authorize]
        public async Task<IActionResult> RemoveMember(string id, string userId)
        {
            try
            {
                await _projectService.RemoveProjectMember(id, userId);
                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost("{id:length(24)}/tasks")]
        [Authorize]
        public async Task<IActionResult> AddTask(string id, CreateAndUpdateTaskRequest task)
        {
            try
            {
                // Validate and convert task status from string to enum
                if (!Enum.TryParse<ProjectTaskStatus>(task.Status, true, out var taskStatus))
                {
                    return BadRequest("Invalid status value.");
                }

                // Create new task object
                var newTask = new ProjectTask
                {
                    Title = task.Title,
                    Description = task.Description,
                    AssignedTo = task.AssignedTo,
                    DueDate = task.DueDate,
                    Status = taskStatus
                };

                // Create task in database and get its ID
                var taskId = await _projectService.CreateTask(newTask);

                // Check if task ID was generated correctly
                if (taskId == null)
                {
                    return StatusCode(500, "Task ID was not generated correctly.");
                }

                // Add task ID to the project
                bool taskAdded = await _projectService.AddTask(id, taskId);
                if (!taskAdded)
                {
                    return StatusCode(500, "Failed to add task to the project.");
                }

                // Return the created task details
                return CreatedAtRoute("GetTask", new { id = taskId }, newTask);
            }
            catch (Exception e)
            {
                // Log the exception (optional) and return a 500 status code with error message
                Console.WriteLine($"Error adding task: {e.Message}");
                return StatusCode(500, "An error occurred while adding the task.");
            }
        }


        [HttpPut("{id:length(24)}/tasks/{taskId:length(24)}")]
        [Authorize]
        public async Task<IActionResult> UpdateTask(string id, string taskId, CreateAndUpdateTaskRequest task)
        {
            try
            {
                if (!Enum.TryParse<ProjectTaskStatus>(task.Status, true, out var taskStatus))
                {
                    return BadRequest("Invalid status value.");
                }

                var newTask = new ProjectTask
                {
                    Title = task.Title,
                    Description = task.Description,
                    AssignedTo = task.AssignedTo,
                    DueDate = task.DueDate,
                    Status = taskStatus
                };
                await _projectService.UpdateTask(taskId, newTask);
                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpDelete("{id:length(24)}/tasks/{taskId:length(24)}")]
        [Authorize]
        public async Task<IActionResult> RemoveTask(string id, string taskId)
        {
            try
            {
                await _projectService.RemoveTask(id, taskId);
                return NoContent();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}