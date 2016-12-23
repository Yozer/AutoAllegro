using AutoAllegro.Models;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroProcessor
    {
        void Init();
        void Process(string userId, long journalStart);
        void StartProcessor(Auction auction);
        void StopProcessor(Auction auction);
    }
}