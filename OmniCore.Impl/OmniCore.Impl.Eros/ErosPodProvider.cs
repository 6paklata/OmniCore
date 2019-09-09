﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmniCore.Model;
using OmniCore.Model.Enums;
using OmniCore.Model.Interfaces;
using OmniCore.Radio.RileyLink;

namespace OmniCore.Impl.Eros
{
    public class ErosPodProvider : IPodProvider<ErosPod>
    {
        private IRadioProvider[] _radioProviders;
        private IPodRepository<ErosPod> _podRepository;
        private IRadioAdapter _radioAdapter;
        private IPodRequestRepository<ErosRequest> _requestRepository;

        private Dictionary<Guid, ErosRequestProcessor> _requestProcessors;

        public ErosPodProvider(IRadioAdapter radioAdapter,
            IRadioProvider[] radioProviders, 
            IPodRepository<ErosPod> podRepository,
            IPodRequestRepository<ErosRequest> requestRepository)
        {
            _radioProviders = radioProviders;
            _radioAdapter = radioAdapter;
            _podRepository = podRepository;
            _requestRepository = requestRepository;

            _requestProcessors = new Dictionary<Guid, ErosRequestProcessor>();
        }

        public async Task<ErosPod> GetActivePod()
        {
            var pods = await _podRepository.GetActivePods();
            return pods.OrderByDescending(p => p.Created).FirstOrDefault();
        }

        public async Task<IEnumerable<ErosPod>> GetActivePods()
        {
            return (await _podRepository.GetActivePods())
                .OrderBy(p => p.Created);
        }

        public async Task Archive(ErosPod pod)
        {
            pod.Archived = true;
            await _podRepository.CreateOrUpdate(pod);
        }

        public async Task<ErosPod> New(IEnumerable<IRadio> radios)
        {
            var pod = new ErosPod
            {
                Id = Guid.NewGuid(),
                ProviderSpecificRadioIds = radios.Select(r => r.ProviderSpecificId).ToArray(),
                RadioAddress = GenerateRadioAddress()
            };
            await _podRepository.CreateOrUpdate(pod);
            return pod;
        }

        public async Task<ErosPod> Register(ErosPod pod, IEnumerable<IRadio> radios)
        {
            pod.ProviderSpecificRadioIds = radios.Select(r => r.ProviderSpecificId).ToArray();
            return await _podRepository.CreateOrUpdate(pod);
        }

        public IObservable<IRadio> ListAllRadios()
        {
            return Observable.Create<IRadio>((IObserver<IRadio> observer) =>
            {
                var disposables = new List<IDisposable>();
                foreach (var radioProvider in _radioProviders)
                {
                    disposables.Add(radioProvider.ListRadios()
                        .Subscribe(radio =>
                        {
                            observer.OnNext(radio);
                        }));
                }

                return Disposable.Create(() =>
                {
                    foreach(var disposable in disposables)
                        disposable.Dispose();

                });
            });
        }

        private uint GenerateRadioAddress()
        {
            var random = new Random();
            var buffer = new byte[3];
            random.NextBytes(buffer);
            uint address = 0x34000000;
            address |= (uint)buffer[0] << 16;
            address |= (uint)buffer[1] << 8;
            address |= (uint)buffer[2];
            return address;
        }

        private ErosRequestProcessor GetProcessor(ErosPod pod)
        {
            lock(_requestProcessors)
            {
                if (!_requestProcessors.ContainsKey(pod.Id))
                {
                    _requestProcessors.Add(pod.Id, new ErosRequestProcessor(pod, _requestRepository));
                }
                return _requestProcessors[pod.Id];
            }
        }

        public async Task QueueRequest(IPodRequest<ErosPod> request)
        {
            try
            {
                await GetProcessor(request.Pod).QueueRequest((ErosRequest)request);
            }
            catch (Exception e)
            {
                throw;
            }

        }

        public async Task<IPodResult<ErosPod>> ExecuteRequest(IPodRequest<ErosPod> request)
        {
            try
            {
                var processor = GetProcessor(request.Pod);
                await processor.QueueRequest((ErosRequest)request);
                return await processor.GetResult((ErosRequest)request, 0);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<IPodResult<ErosPod>> GetResult(IPodRequest<ErosPod> request, int timeout)
        {
                var processor = GetProcessor(request.Pod);
                return await processor.GetResult((ErosRequest)request, 0);
        }

        public async Task<bool> CancelRequest(IPodRequest<ErosPod> request)
        {
                var processor = GetProcessor(request.Pod);
                return await processor.CancelRequest((ErosRequest)request);
        }

        public async Task<IList<IPodRequest<ErosPod>>> GetPendingRequests(ErosPod pod)
        {
                var processor = GetProcessor(pod);
                return (IList<IPodRequest<ErosPod>>)await processor.GetPendingRequests();
        }

        public async Task<IList<IPodRequest<ErosPod>>> GetPendingRequests()
        {
            var processors = new List<ErosRequestProcessor>();
            var list = new List<IPodRequest<ErosPod>>();
            lock(_requestProcessors)
            {
                foreach(var processor in _requestProcessors.Values)
                    processors.Add(processor);
            }

            foreach(var processor in processors)
                list.AddRange((IList<IPodRequest<ErosPod>>)await processor.GetPendingRequests());

            return list;
        }
    }
}
