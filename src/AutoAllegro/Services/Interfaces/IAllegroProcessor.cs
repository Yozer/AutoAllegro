﻿namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroTransactionProcessor
    {
        void Process();
        void Init();
    }
}