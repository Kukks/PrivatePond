using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PrivatePond
{
    public static class WebHostExtensions
    {
        public static async Task StartWithTasksAsync(this IHost webHost, CancellationToken cancellationToken = default)
        {
            // Load all tasks from DI
            var startupTasks = webHost.Services.GetServices<IStartupTask>();

            // Execute all the tasks
            foreach (var startupTask in startupTasks)
            {
                await startupTask.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}