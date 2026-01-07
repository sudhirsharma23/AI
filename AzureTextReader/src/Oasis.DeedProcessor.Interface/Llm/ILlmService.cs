using System.Threading;
using System.Threading.Tasks;

namespace Oasis.DeedProcessor.Interface.Llm
{
    public interface ILlmService
    {
        Task InvokeAfterOcrAsync(CancellationToken cancellationToken = default);
    }
}
