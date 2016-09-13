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
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AzureCat.Samples.GatewayService.DataCollections;
using Microsoft.AzureCat.Samples.ResourceMutexActorService.Interfaces;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

#endregion

namespace Microsoft.AzureCat.Samples.GatewayService.Controllers
{
    public class GatewayController : ApiController
    {
        #region Private Static Fields

        private static readonly Dictionary<string, IResourceMutexActor> ActorProxyDictionary = new Dictionary<string, IResourceMutexActor>();

        #endregion

        #region Private Static Methods

        private static IResourceMutexActor GetActorProxy(string resourceId)
        {
            lock (ActorProxyDictionary)
            {
                if (ActorProxyDictionary.ContainsKey(resourceId))
                {
                    return ActorProxyDictionary[resourceId];
                }
                ActorProxyDictionary[resourceId] = ActorProxy.Create<IResourceMutexActor>(new ActorId(resourceId),
                                                                                          new Uri(GatewayService.ResourceMutextActorServiceUri));
                return ActorProxyDictionary[resourceId];
            }
        }

        #endregion

        #region Private Constants

        //************************************
        // Parameters
        //************************************

        #endregion

        #region Public Methods

        [HttpGet]
        public string Test()
        {
            return "TEST";
        }

        [HttpPost]
        [Route("api/gateway/echo")]
        public string Echo(string value)
        {
            return "value";
        }

        /// <summary>
        /// Initiates an asynchronous operation to acquire the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="payload">Thre payload containing the resource Id, request Id and lease interval</param>
        /// <returns>True if the operation completes successfully, false otherwise.</returns>
        [HttpPost]
        [Route("api/gateway/acquirelease")]
        public async Task<bool> AcquireLeaseAsync(Payload payload)
        {
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(payload?.ResourceId))
                {
                    throw new ArgumentNullException(nameof(payload.ResourceId), "Payload.ResourceId property cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(payload.RequesterId))
                {
                    throw new ArgumentNullException(nameof(payload.RequesterId), "Payload.RequesterId property cannot be null or empty.");
                }

                // Gets actor proxy
                var proxy = GetActorProxy(payload.ResourceId);
                if (proxy == null)
                {
                    return false;
                }

                // Invokes actor using proxy
                ServiceEventSource.Current.Message($"Calling ResourceMutexActor.AcquireLeaseAsync: ResourceId=[{payload.ResourceId}] RequesterId=[{payload.RequesterId}] LeaseInterval=[{payload.LeaseInterval}]...");
                return await proxy.AcquireLeaseAsync(payload.RequesterId, payload.LeaseInterval);
            }
            catch (AggregateException ex)
            {
                if (!(ex.InnerExceptions?.Count > 0))
                {
                    return false;
                }
                foreach (var exception in ex.InnerExceptions)
                {
                    ServiceEventSource.Current.Message(exception.Message);
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message(ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Initiates an asynchronous operation to renew the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="payload">Thre payload containing the resource Id, request Id and lease interval</param>
        /// <returns>True if the operation completes successfully, false otherwise.</returns>
        [HttpPost]
        [Route("api/gateway/renewlease")]
        public async Task<bool> RenewLeaseAsync(Payload payload)
        {
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(payload?.ResourceId))
                {
                    throw new ArgumentNullException(nameof(payload.ResourceId), "Payload.ResourceId property cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(payload.RequesterId))
                {
                    throw new ArgumentNullException(nameof(payload.RequesterId), "Payload.RequesterId property cannot be null or empty.");
                }

                // Gets actor proxy
                var proxy = GetActorProxy(payload.ResourceId);
                if (proxy == null)
                {
                    return false;
                }

                // Invokes actor using proxy
                ServiceEventSource.Current.Message($"Calling ResourceMutexActor.RenewLeaseAsync: ResourceId=[{payload.ResourceId}] RequesterId=[{payload.RequesterId}] LeaseInterval=[{payload.LeaseInterval}]...");
                return await proxy.RenewLeaseAsync(payload.RequesterId, payload.LeaseInterval);
            }
            catch (AggregateException ex)
            {
                if (!(ex.InnerExceptions?.Count > 0))
                {
                    return false;
                }
                foreach (var exception in ex.InnerExceptions)
                {
                    ServiceEventSource.Current.Message(exception.Message);
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message(ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Initiates an asynchronous operation to release the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="payload">Thre payload containing the resource Id, request Id and lease interval</param>
        /// <returns>True if the operation completes successfully, false otherwise.</returns>
        [HttpPost]
        [Route("api/gateway/releaselease")]
        public async Task<bool> ReleaseLeaseAsync(Payload payload)
        {
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(payload?.ResourceId))
                {
                    throw new ArgumentNullException(nameof(payload.ResourceId), "Payload.ResourceId property cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(payload.RequesterId))
                {
                    throw new ArgumentNullException(nameof(payload.RequesterId), "Payload.RequesterId property cannot be null or empty.");
                }

                // Gets actor proxy
                var proxy = GetActorProxy(payload.ResourceId);
                if (proxy == null)
                {
                    return false;
                }

                // Invokes actor using proxy
                ServiceEventSource.Current.Message($"Calling ResourceMutexActor.ReleaseLeaseAsync: ResourceId=[{payload.ResourceId}] RequesterId=[{payload.RequesterId}] LeaseInterval=[{payload.LeaseInterval}]...");
                return await proxy.ReleaseLeaseAsync(payload.RequesterId);
            }
            catch (AggregateException ex)
            {
                if (!(ex.InnerExceptions?.Count > 0))
                {
                    return false;
                }
                foreach (var exception in ex.InnerExceptions)
                {
                    ServiceEventSource.Current.Message(exception.Message);
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message(ex.Message);
            }
            return false;
        }

        #endregion
    }
}