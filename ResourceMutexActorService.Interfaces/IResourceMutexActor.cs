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
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors; 
#endregion

namespace Microsoft.AzureCat.Samples.ResourceMutexActorService.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface IResourceMutexActor : IActor
    {
        /// <summary>
        /// Initiates an asynchronous operation to acquire the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="requesterId">The requester Id.</param>
        /// <param name="leaseInterval">Interval for which the lease is taken on the resource protected by the mutex. 
        /// If the lease is not renewed within this interval, it will cause it to expire and ownership of the resource 
        /// will move to another instance.</param>
        /// <returns>Returns true is the operation succeeds, false otherwise</returns>
        Task<bool> AcquireLeaseAsync(string requesterId, TimeSpan leaseInterval);

        /// <summary>
        /// Initiates an asynchronous operation to renew the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="leaseInterval">Interval for which the lease is taken on the resource protected by the mutex. 
        /// If the lease is not renewed within this interval, it will cause it to expire and ownership of the resource 
        /// will move to another instance.</param>    
        /// <param name="requesterId">The requester Id.</param>
        Task<bool> RenewLeaseAsync(string requesterId, TimeSpan leaseInterval);

        /// <summary>
        /// Initiates an asynchronous operation to release the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="requesterId">The requester Id.</param>
        /// <returns>Returns true is the operation succeeds, false otherwise</returns>
        Task<bool> ReleaseLeaseAsync(string requesterId);
    }
}
