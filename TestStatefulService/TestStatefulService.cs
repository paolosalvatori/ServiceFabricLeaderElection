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
using System.Fabric.Description;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AzureCat.Samples.ResourceMutexActorService.Interfaces;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime; 
#endregion

namespace Microsoft.AzureCat.Samples.TestStatefulService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class TestStatefulService : StatefulService
    {
        #region Private Constants

        //************************************
        // Constants
        //************************************
        private const string ResourceId = "SharedResource";

        //************************************
        // Parameters
        //************************************
        private const string ConfigurationPackage = "Config";
        private const string ConfigurationSection = "ServiceConfig";
        private const string ResourceMutexActorServiceUriParameter = "ResourceMutexActorServiceUri";
        private const string StepCountParameter = "StepCount";
        private const string AcquireIntervalParameter = "AcquireInterval";
        private const string RenewIntervalParameter = "RenewInterval";
        private const string LeaseIntervalParameter = "LeaseInterval";
        private const string DownIntervalParameter = "DownDelay";

        //************************************
        // Default Values
        //************************************
        private const int DefaultStepCountParameter = 5;
        private const int DefaultAcquireIntervalInSeconds = 10;
        private const int DefaultRenewIntervalInSeconds = 10;
        private const int DefaultLeaseIntervalInSeconds = 30;
        private const int DefaultDownIntervalInSeconds = 45;

        #endregion

        #region Private Fields

        /// <summary>
        /// Gets or Sets the Uri of the ResourceMutextActorService
        /// </summary>
        private readonly string resourceMutextActorServiceUri;

        /// <summary>
        /// Gets or Sets the count of the steps after which a leader simulates a down or releases the mutex
        /// </summary>
        private readonly int stepCount;

        /// <summary>
        /// Gets or Sets the acquire interval
        /// </summary>
        private readonly TimeSpan acquireInterval;

        /// <summary>
        /// Gets or Sets the renew interval
        /// </summary>
        private readonly TimeSpan renewInterval;

        /// <summary>
        /// Gets or Sets the lease interval
        /// </summary>
        private readonly TimeSpan leaseInterval;

        /// <summary>
        /// Gets or Sets the time period for which the instance delays to simulate a down
        /// </summary>
        private readonly TimeSpan downInterval;

        #endregion

        #region Public Constructor

        public TestStatefulService(StatefulServiceContext context)
            : base(context)
        {
            try
            {
                // Read settings from the ServiceConfig section in the Settings.xml file
                var activationContext = context.CodePackageActivationContext;
                var config = activationContext.GetConfigurationPackageObject(ConfigurationPackage);
                var section = config.Settings.Sections[ConfigurationSection];

                // Check if a parameter called ResourceMutexActorServiceUriParameter exists in the ServiceConfig config section
                ConfigurationProperty configurationProperty;
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               ResourceMutexActorServiceUriParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[ResourceMutexActorServiceUriParameter];
                    resourceMutextActorServiceUri = !string.IsNullOrWhiteSpace(configurationProperty?.Value)
                                                    ? configurationProperty.Value
                                                    :
                                                    // By default, the current service assumes that if no URI is explicitly defined for the actor service
                                                    // in the Setting.xml file, the latter is hosted in the same Service Fabric application.
                                                    $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                }
                else
                {
                    // By default, the current service assumes that if no URI is explicitly defined for the actor service
                    // in the Setting.xml file, the latter is hosted in the same Service Fabric application.
                    resourceMutextActorServiceUri = $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                }
                ServiceEventSource.Current.Message($"ResourceMutextActorServiceUri=[{resourceMutextActorServiceUri}]");

                // Check if a parameter called StepCountParameter exists in the ServiceConfig config section
                int value;
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               StepCountParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[StepCountParameter];
                    if (!string.IsNullOrWhiteSpace(configurationProperty?.Value) && int.TryParse(configurationProperty.Value, out value))
                    {
                        stepCount = value;
                    }
                    else
                    {
                        stepCount = DefaultStepCountParameter;
                    }
                }
                else
                {
                    stepCount = DefaultStepCountParameter;
                }
                ServiceEventSource.Current.Message($"StepCount=[{stepCount}]");

                // Check if a parameter called AcquireIntervalParameter exists in the ServiceConfig config section
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               AcquireIntervalParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[AcquireIntervalParameter];
                    if (!string.IsNullOrWhiteSpace(configurationProperty?.Value) && int.TryParse(configurationProperty.Value, out value))
                    {
                        acquireInterval = TimeSpan.FromSeconds(value);
                    }
                    else
                    {
                        acquireInterval = TimeSpan.FromSeconds(DefaultAcquireIntervalInSeconds);
                    }
                }
                else
                {
                    acquireInterval = TimeSpan.FromSeconds(DefaultAcquireIntervalInSeconds);
                }
                ServiceEventSource.Current.Message($"AcquireInterval=[{acquireInterval}]");

                // Check if a parameter called RenewIntervalParameter exists in the ServiceConfig config section
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               RenewIntervalParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[RenewIntervalParameter];
                    if (!string.IsNullOrWhiteSpace(configurationProperty?.Value) && int.TryParse(configurationProperty.Value, out value))
                    {
                        renewInterval = TimeSpan.FromSeconds(value);
                    }
                    else
                    {
                        renewInterval = TimeSpan.FromSeconds(DefaultRenewIntervalInSeconds);
                    }
                }
                else
                {
                    renewInterval = TimeSpan.FromSeconds(DefaultRenewIntervalInSeconds);
                }
                ServiceEventSource.Current.Message($"RenewInterval=[{renewInterval}]");

                // Check if a parameter called LeaseIntervalParameter exists in the ServiceConfig config section
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               LeaseIntervalParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[LeaseIntervalParameter];
                    if (!string.IsNullOrWhiteSpace(configurationProperty?.Value) && int.TryParse(configurationProperty.Value, out value))
                    {
                        leaseInterval = TimeSpan.FromSeconds(value);
                    }
                    else
                    {
                        leaseInterval = TimeSpan.FromSeconds(DefaultLeaseIntervalInSeconds);
                    }
                }
                else
                {
                    leaseInterval = TimeSpan.FromSeconds(DefaultLeaseIntervalInSeconds);
                }
                ServiceEventSource.Current.Message($"LeaseInterval=[{leaseInterval}]");

                // Check if a parameter called DownIntervalParameter exists in the ServiceConfig config section
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               DownIntervalParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    configurationProperty = section.Parameters[DownIntervalParameter];
                    if (!string.IsNullOrWhiteSpace(configurationProperty?.Value) && int.TryParse(configurationProperty.Value, out value))
                    {
                        downInterval = TimeSpan.FromSeconds(value);
                    }
                    else
                    {
                        downInterval = TimeSpan.FromSeconds(DefaultDownIntervalInSeconds);
                    }
                }
                else
                {
                    downInterval = TimeSpan.FromSeconds(DefaultDownIntervalInSeconds);
                }
                ServiceEventSource.Current.Message($"DownInterval=[{downInterval}]");
            }
            catch (KeyNotFoundException)
            {
                resourceMutextActorServiceUri = $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                stepCount = DefaultStepCountParameter;
                acquireInterval = TimeSpan.FromSeconds(DefaultAcquireIntervalInSeconds);
                renewInterval = TimeSpan.FromSeconds(DefaultRenewIntervalInSeconds);
                leaseInterval = TimeSpan.FromSeconds(DefaultLeaseIntervalInSeconds);
                downInterval = TimeSpan.FromSeconds(DefaultDownIntervalInSeconds);

                ServiceEventSource.Current.Message("KeyNotFoundException! Using default values for parameters");
                ServiceEventSource.Current.Message($"ResourceMutextActorServiceUri=[{resourceMutextActorServiceUri}]");
                ServiceEventSource.Current.Message($"StepCount=[{stepCount}]");
                ServiceEventSource.Current.Message($"AcquireInterval=[{acquireInterval}]");
                ServiceEventSource.Current.Message($"RenewInterval=[{renewInterval}]");
                ServiceEventSource.Current.Message($"LeaseInterval=[{leaseInterval}]");
                ServiceEventSource.Current.Message($"DownInterval=[{downInterval}]");
            }
        }
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
            return new ServiceReplicaListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actorProxy = ActorProxy.Create<IResourceMutexActor>(new ActorId(ResourceId),
                                                                        new Uri(resourceMutextActorServiceUri));
                if (actorProxy != null)
                {
                    // Create the requesterId
                    var requesterId = $"{Partition.PartitionInfo.Id}_{Context.ReplicaOrInstanceId}";

                    var ok = await actorProxy.AcquireLeaseAsync(requesterId, leaseInterval);
                    if (ok)
                    {
                        ServiceEventSource.Current.Message($"Requester [{requesterId}] acquired a lease on [{ResourceId}] acquired. StepCount=[{stepCount}]");
                        for (var i = 0; i < stepCount; i++)
                        {
                            var step = i + 1;
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] is waiting [{renewInterval.Seconds}] seconds before renewing the lease on [{ResourceId}]. Step [{step}] of [{stepCount}]...");
                            // Wait for time period equal to renewInterval parameter
                            await Task.Delay(renewInterval, cancellationToken);

                            // Renew the lease
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] renewing the lease on [{ResourceId}]. Step [{step}] of [{stepCount}]...");
                            await actorProxy.RenewLeaseAsync(requesterId, leaseInterval);
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] successfully renewed the lease on [{ResourceId}]. Step [{step}] of [{stepCount}].");
                        }

                        // Simulate a down or mutex release
                        var random = new Random();
                        var value = random.Next(1, 3);
                        if (value == 1)
                        {
                            // Simulate a down period
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] simulating a down of [{downInterval.Seconds}] seconds...");
                            await Task.Delay(downInterval, cancellationToken);
                        }
                        else
                        {
                            // Release the mutex lease
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] releasing the lease on [{ResourceId}]...");
                            await actorProxy.ReleaseLeaseAsync(requesterId);
                            ServiceEventSource.Current.Message($"Requester [{requesterId}] successfully released the lease on [{ResourceId}]");
                        }
                    }
                    
                    // Wait before retrying to acquire the lease
                    ServiceEventSource.Current.Message($"Requester [{requesterId}] is waiting [{acquireInterval.Seconds}] seconds before retrying to acquire a lease on [{ResourceId}]...");
                    await Task.Delay(acquireInterval, cancellationToken);
                }
                else
                {
                    throw new ApplicationException($"The ActorProxy cannot be null. ResourceId=[{ResourceId}] ResourceMutextActorServiceUri=[{resourceMutextActorServiceUri}]");
                }
            }
            // ReSharper disable once FunctionNeverReturns
        }

        #endregion
    }
}
