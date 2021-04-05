using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace PrivatePond
{
    public class StartupTaskRunner : IHostedService
    {
        private readonly IEnumerable<IStartupTask> _startupTasks;

        public StartupTaskRunner(IEnumerable<IStartupTask> startupTasks)
        {
            _startupTasks = startupTasks;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(_startupTasks.Select(task => task.ExecuteAsync(cancellationToken)));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}