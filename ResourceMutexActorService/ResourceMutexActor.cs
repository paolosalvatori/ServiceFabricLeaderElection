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
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.AzureCat.Samples.ResourceMutexActorService.Interfaces;

#endregion

namespace Microsoft.AzureCat.Samples.ResourceMutexActorService
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [ActorService(Name = "ResourceMutexActorService")]
    [StatePersistence(StatePersistence.Persisted)]
    internal class ResourceMutexActor : Actor, IRemindable, IResourceMutexActor
    {
        #region Private Constants

        //************************************
        // Actor States
        //************************************
        private const string LeaderIdState = "leaderId";
        private const string LeaseIntervalState = "leaseInterval";
        private const string LeaseDateTimeState = "leaseDateTime";

        //************************************
        // Reminders
        //************************************
        private const string ReleaseResourceMutexReminder = "releaseReminder";

        #endregion

        #region Private Fields
        private int maxRetryCount = 3;
        #endregion

        #region Actor Overridden Methods

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, $"ResourceMutexActor [{Id}] Activated");
            return Task.FromResult(0);
        }
        #endregion

        #region IResourceMutexActor Methods
        /// <summary>
        /// Initiates an asynchronous operation to acquire the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="requesterId">The requester Id.</param>
        /// <param name="leaseInterval">Interval for which the lease is taken on the resource protected by the mutex. 
        /// If the lease is not renewed within this interval, it will cause it to expire and ownership of the resource 
        /// will move to another instance.</param>
        /// <returns>Returns true is the operation succeeds, false otherwise</returns>
        public async Task<bool> AcquireLeaseAsync(string requesterId, TimeSpan leaseInterval)
        {
            try
            {
                // Validate parameter
                if (string.IsNullOrWhiteSpace(requesterId))
                {
                    throw new ArgumentNullException(nameof(requesterId), "requesterId argument cannot be null or empty.");
                }

                if (leaseInterval.TotalSeconds <= 0)
                {
                    throw new ArgumentException("leaseInterval cannot be less or equal to zero.", nameof(leaseInterval));
                }

                var result = await StateManager.TryGetStateAsync<string>(LeaderIdState);
                DateTime now;
                if (result.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(result.Value))
                    {
                        // The resource is already acquired. Return true if requesterId == leaderId, false otherwise
                        if (string.Compare(requesterId, result.Value, StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            // The acquire lease operation is idempotent. Add or update the leaseInterval and leaseDateTime states.
                            now = DateTime.UtcNow;
                            await StateManager.AddOrUpdateStateAsync(LeaseIntervalState, leaseInterval, (k, v) => leaseInterval);
                            await StateManager.AddOrUpdateStateAsync(LeaseDateTimeState, now, (k, v) => now);

                            // Register a one-shot reminder
                            for (var i = 0; i < maxRetryCount; i++)
                            {
                                try
                                {
                                    await RegisterReminderAsync(ReleaseResourceMutexReminder,
                                                                null,
                                                                leaseInterval,
                                                                TimeSpan.FromMilliseconds(-1));
                                }
                                catch (FabricTransientException)
                                {
                                }
                            }

                            ActorEventSource.Current.Message($"Operation succeeded. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                            return true;
                        }

                        // The resource mutex cannot be acquired by a requester other than the existing leader
                        ActorEventSource.Current.Message($"Operation failed. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                        return false;
                    }
                }
                
                // The resource is not acquired yet. Save leaderId == requesterId and return true.
                await StateManager.AddOrUpdateStateAsync(LeaderIdState, requesterId, (k, v) => requesterId);

                // Add or update the leaseInterval and leaseDateTime states.
                
                await StateManager.AddOrUpdateStateAsync(LeaseIntervalState, leaseInterval, (k, v) => leaseInterval);
                now = DateTime.UtcNow;
                await StateManager.AddOrUpdateStateAsync(LeaseDateTimeState, now, (k, v) => now);

                // Register a one-shot reminder
                for (var i = 0; i < maxRetryCount; i++)
                {
                    try
                    {
                            await RegisterReminderAsync(ReleaseResourceMutexReminder,
                                                        null,
                                                        leaseInterval,
                                                        TimeSpan.FromMilliseconds(-1));
                    }
                    catch (FabricTransientException)
                    {
                    }
                }

                ActorEventSource.Current.Message($"Operation succeeded. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[NULL]");
                return true;
            }
            catch (Exception ex)
            {
                ActorEventSource.Current.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Initiates an asynchronous operation to renew the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="requesterId">The requester Id.</param>
        /// <param name="leaseInterval">Interval for which the lease is taken on the resource protected by the mutex. 
        /// If the lease is not renewed within this interval, it will cause it to expire and ownership of the resource 
        /// will move to another instance.</param>
        /// <returns>Returns true is the operation succeeds, false otherwise</returns>
        public async Task<bool> RenewLeaseAsync(string requesterId, TimeSpan leaseInterval)
        {
            try
            {
                // Validate parameter
                if (string.IsNullOrWhiteSpace(requesterId))
                {
                    throw new ArgumentNullException(nameof(requesterId), "requesterId argument cannot be null or empty.");
                }

                if (leaseInterval.TotalSeconds <= 0)
                {
                    throw new ArgumentException("leaseInterval cannot be less or equal to zero.", nameof(leaseInterval));
                }

                var result = await StateManager.TryGetStateAsync<string>(LeaderIdState);
                DateTime now;
                if (result.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(result.Value))
                    {
                        // The resource is already acquired.
                        // Return true if requesterId == leaderId, false otherwise

                        if (string.Compare(requesterId, result.Value, StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            // Renew the lease. Add or update the leaseInterval and leaseDateTime states.
                            now = DateTime.UtcNow;
                            await StateManager.AddOrUpdateStateAsync(LeaseIntervalState, leaseInterval, (k, v) => leaseInterval);
                            await StateManager.AddOrUpdateStateAsync(LeaseDateTimeState, now, (k, v) => now);

                            // Register a one-shot reminder
                            for (var i = 0; i < maxRetryCount; i++)
                            {
                                try
                                {
                                    await RegisterReminderAsync(ReleaseResourceMutexReminder,
                                                                null,
                                                                leaseInterval,
                                                                TimeSpan.FromMilliseconds(-1));
                                }
                                catch (FabricTransientException)
                                {
                                }
                            }

                            ActorEventSource.Current.Message($"Operation succeeded. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                            return true;
                        }

                        // The resource mutex cannot be renewed by a requester other than the leader
                        ActorEventSource.Current.Message($"Operation failed. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                        return false;
                    }
                }
                
                // If the value of the leaderId state is null, the renew operation acquires a lease on the resource
                await StateManager.AddOrUpdateStateAsync(LeaderIdState, requesterId, (k, v) => requesterId);

                // Add or update the leaseInterval and leaseDateTime states.
                await StateManager.AddOrUpdateStateAsync(LeaseIntervalState, leaseInterval, (k, v) => leaseInterval);
                now = DateTime.UtcNow;
                await StateManager.AddOrUpdateStateAsync(LeaseDateTimeState, now, (k, v) => now);

                // Register a one-shot reminder
                for (var i = 0; i < maxRetryCount; i++)
                {
                    try
                    {
                        await RegisterReminderAsync(ReleaseResourceMutexReminder,
                                                    null,
                                                    leaseInterval,
                                                    TimeSpan.FromMilliseconds(-1));
                    }
                    catch (FabricTransientException)
                    {
                    }
                }

                ActorEventSource.Current.Message($"Operation succeeded. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[NULL]");
                return true;
            }
            catch (Exception ex)
            {
                ActorEventSource.Current.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Initiates an asynchronous operation to release the lease on 
        /// a mutex that governs the exclusive access to a resource.
        /// </summary>
        /// <param name="requesterId">The requester Id.</param>
        /// <returns>Returns true is the operation succeeds, false otherwise</returns>
        public async Task<bool> ReleaseLeaseAsync(string requesterId)
        {
            try
            {
                // Validate parameter
                if (string.IsNullOrWhiteSpace(requesterId))
                {
                    throw new ArgumentNullException(nameof(requesterId), "requesterId argument cannot be null or empty.");
                }

                var result = await StateManager.TryGetStateAsync<string>(LeaderIdState);
                if (result.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(result.Value))
                    {
                        // The resource mutex is released only if the resource is  acquired by requesterId.
                        if (string.Compare(requesterId, result.Value, StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            // Remove all the states
                            if (!await StateManager.TryRemoveStateAsync(LeaderIdState) ||
                                !await StateManager.TryRemoveStateAsync(LeaseIntervalState) ||
                                !await StateManager.TryRemoveStateAsync(LeaseDateTimeState))
                            {
                                return false;
                            }

                            // The resource mutex is released as the max retry count has been exhausted
                            ActorEventSource.Current.Message($"Operation succeeded. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                            return true;
                        }
                        
                        // The resource mutex cannot be released by another requester other than the leader
                        ActorEventSource.Current.Message($"Operation failed. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[{result.Value}]");
                        return false;
                    }
                }

                // The resource mutex cannot be released by another requester other than the leader
                ActorEventSource.Current.Message($"Operation failed. Resource=[{Id}] RequesterId=[{requesterId}] LeaderId=[NULL]");
                return false;
            }
            catch (Exception ex)
            {
                ActorEventSource.Current.Error(ex);
                throw;
            }
        }

        #endregion

        #region IRemindable Methods

        public async Task ReceiveReminderAsync(string reminderName, byte[] context, TimeSpan dueTime, TimeSpan period)
        {
            try
            {
                var resultLeaderIdState = await StateManager.TryGetStateAsync<string>(LeaderIdState);
                var resultLeaseIntervalState = await StateManager.TryGetStateAsync<TimeSpan>(LeaseIntervalState);
                var resultLeaseDateTimeState = await StateManager.TryGetStateAsync<DateTime>(LeaseDateTimeState);
                if (resultLeaderIdState.HasValue &&
                    resultLeaseIntervalState.HasValue &&
                    resultLeaseDateTimeState.HasValue)
                {
                    var leaderId = resultLeaderIdState.Value;
                    var leaseInterval = resultLeaseIntervalState.Value;
                    var leaseDateTime = resultLeaseDateTimeState.Value;
                    
                    if ((DateTime.UtcNow - leaseDateTime).TotalSeconds > leaseInterval.TotalSeconds)
                    {
                        // Remove all the states
                        if (await StateManager.TryRemoveStateAsync(LeaderIdState) &&
                            await StateManager.TryRemoveStateAsync(LeaseIntervalState) &&
                            await StateManager.TryRemoveStateAsync(LeaseDateTimeState))
                        {
                            // The resource mutex is released as the max retry count has been exhausted
                            ActorEventSource.Current.Message($"Resource Mutex Released. Resource=[{Id}] LeaderId=[{leaderId}].");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ActorEventSource.Current.Error(ex);
            }
        } 

        #endregion
    }
}
