#region Copyright
//=======================================================================================
// Microsoft Azure Customer Advisory Team  
//
// This sample is supplemental to the technical guidance published on the community
// blog at http://blogs.msdn.com/b/paolos/. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright © 2016 Microsoft Corporation. All rights reserved.
// 
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER 
// EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF 
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE. YOU BEAR THE RISK OF USING IT.
//=======================================================================================
#endregion

#region Using Directives
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using StatefulService = Microsoft.ServiceFabric.Services.Runtime.StatefulService;
#endregion

namespace Microsoft.AzureCat.Samples.GarbageCollectionService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class GarbageCollectionService : StatefulService, IGarbageCollection
    {
        #region Private Constants

        //************************************
        // Public Constants
        //************************************
        public const string ServiceReplicaListenersCreated = "Service replica listeners created.";
        public const string ActorsToDelete = "ActorsToDelete";

        //************************************
        // Parameters
        //************************************
        private const string ConfigurationPackage = "Config";
        private const string ConfigurationSection = "FrameworkConfig";
        private const string GarbageCollectionIntervalParameter = "GarbageCollectionInterval";

        //************************************
        // Default Values
        //************************************
        private const int DefaultGarbageCollectionInterval = 60;

        #endregion

        #region Private Fields
        private TimeSpan garbageCollectionInterval = TimeSpan.FromSeconds(DefaultGarbageCollectionInterval);
        #endregion

        #region Public Constructor
        public GarbageCollectionService(StatefulServiceContext context)
            : base(context)
        { }
        #endregion

        #region StatefulService Overridden Methods
        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see http://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            ServiceEventSource.Current.Message(ServiceReplicaListenersCreated);
            return new[]
            {
                new ServiceReplicaListener(this.CreateServiceRemotingListener)
            };
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var actorsToDeleteDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, ActorInfo>>(ActorsToDelete);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var transaction = StateManager.CreateTransaction())
                {
                    var enumerable = await actorsToDeleteDictionary.CreateEnumerableAsync(transaction);
                    using (var enumerator = enumerable.GetAsyncEnumerator())
                    {
                        var taskList = new List<Task>();
                        while (await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var actorInfo = enumerator.Current.Value;
                            taskList.Add(DeleteActorAsync(actorInfo.ServiceUri, actorInfo.ActorId, cancellationToken));
                        }
                        await Task.WhenAll(taskList);
                    }
                    await transaction.CommitAsync();
                }
                await Task.Delay(garbageCollectionInterval, cancellationToken);
            }
            // ReSharper disable once FunctionNeverReturns
        }
        #endregion

        #region IGarbageCollection Methods
        public async Task DeleteActorAsync(Uri serviceUri, ActorId actorId)
        {
            try
            {
                if (serviceUri == null || actorId == null)
                {
                    return;
                }
                var actorsToDeleteDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, ActorInfo>>(ActorsToDelete);
                var key = $"{serviceUri.AbsoluteUri}_{actorId.GetStringId()}";
                var actorInfo = new ActorInfo
                {
                    ServiceUri = serviceUri,
                    ActorId = actorId   
                };
                using (var transaction = StateManager.CreateTransaction())
                {
                    await actorsToDeleteDictionary.AddOrUpdateAsync(transaction, key, actorInfo, (k, a) => a);
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Error(ex);
                throw;
            }
        }
        #endregion

        #region Private Methods
        public void Initialize(StatefulServiceContext parameters)
        {
            if (parameters == null)
            {
                return;
            }

            try
            {
                // Read settings from the DeviceActorServiceConfig section in the Settings.xml file
                var activationContext = parameters.CodePackageActivationContext;
                var config = activationContext.GetConfigurationPackageObject(ConfigurationPackage);
                var section = config.Settings.Sections[ConfigurationSection];

                // Read the GarbageCollectionInterval setting from the Settings.xml file
                if (section.Parameters.Any(
                    p => string.Compare(
                        p.Name,
                        GarbageCollectionIntervalParameter,
                        StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    var parameter = section.Parameters[GarbageCollectionIntervalParameter];
                    int seconds;
                    if (!string.IsNullOrWhiteSpace(parameter?.Value) && int.TryParse(parameter.Value, out seconds))
                    {
                        garbageCollectionInterval = TimeSpan.FromSeconds(seconds);
                    }
                    else
                    {
                        garbageCollectionInterval = TimeSpan.FromSeconds(DefaultGarbageCollectionInterval);
                    }
                }
            }
            catch (KeyNotFoundException)
            {
            }
            ServiceEventSource.Current.Message($"{GarbageCollectionIntervalParameter} = [{garbageCollectionInterval}] seconds");
        }

        private async Task DeleteActorAsync(Uri serviceUri, ActorId actorId, CancellationToken token)
        {
            var actorServiceProxy = ActorServiceProxy.Create(serviceUri, actorId);
            await actorServiceProxy.DeleteActorAsync(actorId, token);
        }
        #endregion
    }
}
