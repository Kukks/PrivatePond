using System.Threading;
using System.Threading.Tasks;

namespace PrivatePond
{
    public interface IStartupTask
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }
}