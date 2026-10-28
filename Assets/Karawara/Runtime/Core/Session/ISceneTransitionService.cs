using System.Threading;
using System.Threading.Tasks;
namespace Karawara.Core.Session
{
    public interface ISceneTransitionService
    {
        Task<SessionResult> LoadAsync(string scenePath, CancellationToken cancellationToken);
    }
}
