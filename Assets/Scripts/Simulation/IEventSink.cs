namespace Scp.Simulation
{
    public interface IEventSink
    {
        void Emit(DomainEvent domainEvent);
    }
}
