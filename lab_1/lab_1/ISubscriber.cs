using System.Threading.Tasks;

namespace lab_1
{
    public interface ISubscriber
    {
        string Name { get; }
        bool IsAvailable { get; set; }
        Task HandleEventAsync(Event evt);
    }
}