using System;
using System.Linq;
using System.ServiceModel;
using AutoAllegro.Data;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services.AllegroProcessors
{
    public interface IAllegroAbstractProcessor
    {
        void Init();
        void Process();
    }

    public abstract class AllegroAbstractProcessor<T>  where T: IAllegroAbstractProcessor
    {
        private readonly IBackgroundJobClient _backgroundJob;
        private readonly ILogger _logger;
        private readonly TimeSpan _interval;

        protected AllegroAbstractProcessor(IBackgroundJobClient backgroundJob, ILogger logger, TimeSpan interval)
        {
            _backgroundJob = backgroundJob;
            _logger = logger;
            _interval = interval;
        }
        public virtual void Init()
        {
            _backgroundJob.Schedule<T>(t => t.Process(), _interval);
        }

        public void Process()
        {
            try
            {
                Execute();
            }
            catch (TimeoutException e)
            {
                _logger.LogError(1, e, "The service operation timed out.");
            }
            catch (FaultException e)
            {
                _logger.LogError(1, e, "An unknown exception was received.");
            }
            catch (CommunicationException e)
            {
                _logger.LogError(1, e, "There was a communication problem.");
            }

            _backgroundJob.Schedule<T>(t => t.Process(), _interval);
        }

        protected abstract void Execute();
        protected AllegroCredentials GetAllegroCredentials(ApplicationDbContext db, string id)
        {
            var user = db.Users.Single(t => t.Id == id);
            return new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
        }
    }
}
