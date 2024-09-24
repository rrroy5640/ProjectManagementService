using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using ProjectManagementService.DTOs;
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
        private readonly SqsService _sqsService;

        public ProjectController(ProjectService projectService, SqsService sqsService)
        {
            _projectService = projectService;
            _sqsService = sqsService;
        }

        private async Task<bool> UserHasAccessToProject(string projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }
            return await _projectService.UserHasAccessToProject(userId, projectId);
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var taskIds = new List<string>();

            if (project.Tasks != null && project.Tasks.Any())
            {
                foreach (var taskRequest in project.Tasks)
                {
                    if (!Enum.TryParse<ProjectTaskStatus>(taskRequest.Status, true, out var taskStatus))
                    {
                        ModelState.AddModelError($"Tasks[{project.Tasks.IndexOf(taskRequest)}].Status", "Invalid task status value.");
                        return BadRequest(ModelState);
                    }

                    var newTask = new ProjectTask
                    {
                        Title = taskRequest.Title,
                        Description = taskRequest.Description,
                        AssignedTo = taskRequest.AssignedTo,
                        DueDate = taskRequest.DueDate,
                        Status = taskStatus
                    };

                    var taskId = await _projectService.CreateTask(newTask);
                    if (string.IsNullOrEmpty(taskId))
                    {
                        return StatusCode(500, "Failed to create one of the tasks.");
                    }

                    taskIds.Add(taskId);
                }
            }

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

            var createdProject = await _projectService.CreateProject(newProject);
            if (createdProject == null)
            {
                return StatusCode(500, "Failed to create the project.");
            }

            var projectDTO = MapProjectToDTO(createdProject);

            var sqsMessage = new SqsMessageDto
            {
                MessageType = "ProjectCreated",
                Payload = projectDTO
            };

            await _sqsService.SendMessageAsync(sqsMessage);

            return CreatedAtRoute("GetProject", new { id = projectDTO.Id }, projectDTO);
        }

        private static ProjectDTO MapProjectToDTO(Project project)
        {
            return new ProjectDTO
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
        }

        [HttpGet("{id:length(24)}", Name = "GetProject")]
        [Authorize]
        public async Task<ActionResult<ProjectDTO>> GetProjectById(string id)
        {
            if (!await UserHasAccessToProject(id))
            {
                return Forbid();
            }

            var project = await _projectService.GetProjectById(id);
            if (project == null)
            {
                return NotFound();
            }

            return Ok(MapProjectToDTO(project));
        }

        [HttpPut("{id:length(24)}")]
        [Authorize]
        public async Task<ActionResult<Project>> UpdateProject(string id, UpdateProjectRequest project)
        {
            try
            {
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

                var projectIn = await _projectService.GetProjectById(id);

                if (projectIn == null)
                {
                    return NotFound($"Project with ID {id} not found.");
                }

                projectIn.Name = project.Name;
                projectIn.Description = project.Description;
                projectIn.StartDate = project.StartDate;
                projectIn.EndDate = project.EndDate;
                projectIn.ProjectMembers = project.ProjectMemberIds;
                projectIn.ProjectOwner = project.ProjectOwner;

                var updatedProject = await _projectService.UpdateProject(id, projectIn);

                if (updatedProject == null)
                {
                    return StatusCode(500, "Failed to update the project.");
                }

                var projectDTO = MapProjectToDTO(updatedProject);

                var sqsMessage = new SqsMessageDto
                {
                    MessageType = "ProjectUpdated",
                    Payload = projectDTO
                };

                await _sqsService.SendMessageAsync(sqsMessage);
                return Ok(updatedProject);
            }
            catch (Exception e)
            {
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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

                await _projectService.AddProjectMember(id, userId);

                var sqsMessage = new SqsMessageDto
                {
                    MessageType = "MemberAddedToProject",
                    Payload = new { ProjectId = id, UserId = userId }
                };

                await _sqsService.SendMessageAsync(sqsMessage);

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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

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

                var taskId = await _projectService.CreateTask(newTask);

                if (taskId == null)
                {
                    return StatusCode(500, "Task ID was not generated correctly.");
                }

                bool taskAdded = await _projectService.AddTask(id, taskId);
                if (!taskAdded)
                {
                    return StatusCode(500, "Failed to add task to the project.");
                }

                var projectDTO = MapProjectToDTO(await _projectService.GetProjectById(id));

                var sqsMessage = new SqsMessageDto
                {
                    MessageType = "TaskAddedToProject",
                    Payload = new { ProjectId = id, TaskId = taskId }
                };

                return CreatedAtRoute("GetTask", new { id = taskId }, newTask);
            }
            catch (Exception e)
            {
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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

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

                var projectDTO = MapProjectToDTO(await _projectService.GetProjectById(id));

                var sqsMessage = new SqsMessageDto
                {
                    MessageType = "TaskUpdated",
                    Payload = new { ProjectId = id, TaskId = taskId }
                };
                await _sqsService.SendMessageAsync(sqsMessage);

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
                if (!await UserHasAccessToProject(id))
                {
                    return Unauthorized();
                }

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