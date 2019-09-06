﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OmniCore.Model.Enums;

namespace OmniCore.Model.Interfaces
{
    public interface IPodProvider<T> where T : IPod, new()
    {
        Task<T> GetActivePod();
        Task<IEnumerable<T>> GetActivePods();
        Task Archive(T pod);
        Task<T> New(IEnumerable<IRadio> radios);
        Task<T> Register(T pod, IEnumerable<IRadio> radios);
        Task QueueRequest(IPodRequest<T> request);
        Task<IPodResult<T>> ExecuteRequest(IPodRequest<T> request);
        Task<IPodResult<T>> GetResult(IPodRequest<T> request, int timeout);
        Task<IPodResult<T>> CancelRequest(IPodRequest<T> request);
        Task CancelRequests(T pod);
        Task CancelAllRequests();
        IObservable<IRadio> ListAllRadios();
    }
}
