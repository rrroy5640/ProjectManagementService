using MongoDB.Driver;
using ProjectManagementService.Models;

namespace ProjectManagementService.Services
{
    public class ProjectService
    {
        private readonly IMongoCollection<Project> _projects;
        private readonly IMongoCollection<UserInfo> _users;
        private readonly IMongoCollection<ProjectTask> _tasks;

        public ProjectService(IMongoDBSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);

            _projects = database.GetCollection<Project>("Projects");
            _tasks = database.GetCollection<ProjectTask>("Tasks");
            _users = database.GetCollection<UserInfo>("Users");
        }

        public async Task<bool> UserHasAccessToProject(string userId, string projectId)
        {
            var project = await GetProjectById(projectId);
            if (project == null)
            {
                return false;
            }

            return project.ProjectOwner == userId || project.ProjectMembers.Contains(userId);
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _projects.Find(project => true).ToListAsync();
        }

        public async Task<Project> CreateProject(Project project)
        {
            await _projects.InsertOneAsync(project);
            return project;
        }

        public async Task<Project> GetProjectById(string id)
        {
            return await _projects.Find(project => project.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Project>> GetProjectsByOwner(string ownerId)
        {
            return await _projects.Find(project => project.ProjectOwner == ownerId).ToListAsync();
        }

        public async Task<Project> UpdateProject(string id, Project projectIn)
        {
            await _projects.ReplaceOneAsync(project => project.Id == id, projectIn);
            return projectIn;
        }

        public async Task RemoveProject(string id)
        {
            await _projects.DeleteOneAsync(project => project.Id == id);
        }

        public async Task AddProjectMember(string projectId, string userId)
        {
            var project = await GetProjectById(projectId);
            var user = await _users.Find(user => user.Id == userId).FirstOrDefaultAsync();

            if (project == null)
            {
                throw new Exception("Project not found.");
            }

            if (user == null)
            {
                throw new Exception("User not found.");
            }

            if (project.ProjectMembers == null)
            {
                project.ProjectMembers = new List<string>();
            }

            project.ProjectMembers.Add(userId);
            await UpdateProject(projectId, project);
        }

        public async Task RemoveProjectMember(string projectId, string userId)
        {
            var project = await GetProjectById(projectId);

            if (project == null)
            {
                throw new Exception("Project not found.");
            }

            if (project.ProjectMembers == null || !project.ProjectMembers.Contains(userId))
            {
                throw new Exception("User not a member of the project.");
            }

            project.ProjectMembers.Remove(userId);
            await UpdateProject(projectId, project);
        }

        public async Task<string?> CreateTask(ProjectTask task)
        {
            try
            {
                await _tasks.InsertOneAsync(task);
                return task.Id;
            }
            catch (Exception e)
            {
                throw new Exception("Error creating task: " + e.Message);
            }
        }

        public async Task<bool> AddTask(string projectId, string taskId)
        {
            var project = await GetProjectById(projectId);
            if (project == null)
            {
                throw new Exception("Project not found.");
            }

            if (project.TaskIds == null)
            {
                project.TaskIds = new List<string>();
            }

            project.TaskIds.Add(taskId);
            var update = Builders<Project>.Update.Set(p => p.TaskIds, project.TaskIds);
            var result = await _projects.UpdateOneAsync(p => p.Id == projectId, update);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveTask(string projectId, string taskId)
        {
            var project = await GetProjectById(projectId);
            if (project == null)
            {
                throw new Exception("Project not found.");
            }

            if (project.TaskIds == null || !project.TaskIds.Contains(taskId))
            {
                throw new Exception("Task not found in the project.");
            }

            project.TaskIds.Remove(taskId);
            _tasks.DeleteOne(t => t.Id == taskId);

            var update = Builders<Project>.Update.Set(p => p.TaskIds, project.TaskIds);
            var result = await _projects.UpdateOneAsync(p => p.Id == projectId, update);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateTask(string taskId, ProjectTask taskIn)
        {
            var task = await _tasks.Find(t => t.Id == taskId).FirstOrDefaultAsync();
            if (task == null)
            {
                throw new Exception("Task not found.");
            }

            var update = Builders<ProjectTask>.Update
                .Set(t => t.Title, taskIn.Title)
                .Set(t => t.Description, taskIn.Description)
                .Set(t => t.AssignedTo, taskIn.AssignedTo)
                .Set(t => t.DueDate, taskIn.DueDate)
                .Set(t => t.Status, taskIn.Status);

            var result = await _tasks.UpdateOneAsync(t => t.Id == taskId, update);

            return result.ModifiedCount > 0;
        }
    }
}
