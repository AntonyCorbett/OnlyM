using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OnlyM.PubSubMessages;

/// <summary>
/// Requests a manual-order reset and returns the task so the caller can await completion.
/// </summary>
internal sealed class ResetManualOrderMessage : RequestMessage<Task>
{
}
