---
services: service-fabric
platforms: dotnet
author: paolosalvatori
---
# Leader Election Pattern #
Coordinate the actions performed by a collection of collaborating task instances in a distributed application by electing one instance as the leader that assumes responsibility for managing the other instances. This pattern can help to ensure that task instances do not conflict with each other, cause contention for shared resources, or inadvertently interfere with the work that other task instances are performing.   

# Context and Problem #
A typical cloud application consists of many tasks acting in a coordinated manner. These tasks could all be instances running the same code and requiring access to the same resources, or they might be working together in parallel to perform the individual parts of a complex calculation.
The task instances might run autonomously for much of the time, but it may also be necessary to coordinate the actions of each instance to ensure that they don’t conflict, cause contention for shared resources, or inadvertently interfere with the work that other task instances are performing. For example:

- In a cloud-based system that implements horizontal scaling, multiple instances of the same task could be running simultaneously with each instance servicing a different user. If these instances write to a shared resource, it may be necessary to coordinate their actions to prevent each instance from blindly overwriting the changes made by the others.
- If the tasks are performing individual elements of a complex calculation in parallel, the results will need to be aggregated when they all complete. Because the task instances are all peers, there is no natural leader that can act as the coordinator or aggregator.

In this context, the tasks competing for the exclusive access to a shared resource can be heterogeneous:

- Service Fabric reliable services
- Worker Role instances
- Web Jobs
- External applications
- On-premises applications
- etc.

# Solution #
A single task instance should be elected to act as the leader, and this instance should coordinate the actions of the other subordinate task instances. If all of the task instances are running the same code, they could all be capable of acting as the leader. Therefore, the election process must be managed carefully to prevent two or more instances taking over the leader role at the same time.
The system must provide a robust mechanism for selecting the leader. This mechanism must be able to cope with events such as network outages or process failures. In many solutions, the subordinate task instances monitor the leader through some type of heartbeat mechanism, or by polling. If the designated leader terminates unexpectedly, or a network failure renders the leader inaccessible by the subordinate task instances, it will be necessary for them to elect a new leader.
There are several strategies available for electing a leader amongst a set of tasks in a distributed environment, including:

- Selecting the task instance with the lowest-ranked instance or process ID.
- Racing to obtain a shared distributed mutex. The first task instance that acquires the mutex is the leader. However, the system must ensure that, if the leader terminates or becomes disconnected from the rest of the system, the mutex is released to allow another task instance to become the leader.
- Implementing one of the common leader election algorithms such as the Bully Algorithm or the Ring Algorithm. These algorithms are relatively straightforward, but there are also a number of more sophisticated techniques available. These algorithms assume that each candidate participating in the election has a unique ID, and that they can communicate with the other candidates in a reliable manner.

# Issues and Considerations #
Consider the following points when deciding how to implement this pattern:

- The process of electing a leader should be resilient to transient and persistent failures.
- It must be possible to detect when the leader has failed or has become otherwise unavailable (perhaps due to a communications failure). The speed at which such detection is required will be system dependent. Some systems may be able to function for a short while without a leader, during which time a transient fault that caused the leader to become unavailable may have been rectified. In other cases, it may be necessary to detect leader failure immediately and trigger a new election.
- In a system that implements horizontal autoscaling, the leader could be terminated if the system scales back and shuts down some of the computing resources.
- Using a shared distributed mutex introduces a dependency on the availability of the external service that provides the mutex. This service may constitute a single point of failure. If this service should become unavailable for any reason, the system will not be able to elect a leader.
- Using a single dedicated process as the leader is a relatively straightforward approach. However, if the process fails there may be a significant delay while it is restarted, and the resultant latency may affect the performance and response times of other processes if they are waiting for the leader to coordinate an operation.
- Implementing one of the leader election algorithms manually provides the greatest flexibility for tuning and optimizing the code.

# When to Use this Pattern #
Use this pattern when the tasks in a distributed application, such as a cloud-hosted solution, require careful coordination and there is no natural leader.
Note: avoid making the leader a bottleneck in the system. The purpose of the leader is to coordinate the work performed by the subordinate tasks, and it does not necessarily have to participate in this work itself—although it should be capable of doing so if the task is not elected as the leader.

This pattern might not be suitable:

- If there is a natural leader or dedicated process that can always act as the leader. For example, it may be possible to implement a singleton process that coordinates the task instances. If this process fails or becomes unhealthy, the system can shut it down and restart it.
- If the coordination between tasks can be easily achieved by using a more lightweight mechanism. For example, if several task instances simply require coordinated access to a shared resource, a preferable solution might be to use optimistic or pessimistic locking to control access to that resource.
- If a third-party solution is more appropriate. For example, the Microsoft Azure HDInsight service (based on Apache Hadoop) uses the services provided by Apache Zookeeper to coordinate the map/reduce tasks that aggregate and summarize data. It’s also possible to install and configure Zookeeper on a Azure Virtual Machine and integrate it into your own solutions, or use the Zookeeper prebuilt virtual machine image available from Microsoft Open Technologies. For more information, see Apache Zookeeper on Microsoft Azure on the Microsoft Open Technologies website.

# Implementation #
This demo demonstrates how to implement the Leader Election pattern using a Service Fabric Actor service. This service can be used to coordinate the access to a  set of shared recources, each identified by the ActorId, across a range of Service Fabric services and external services that can invoke the methods exposed by the ResourceMutexActor via a REST service hosted by a Stateless Web API service.

The Service Fabric actor model supports a strict, turn-based model for invoking actor methods. This means that no more than one thread can be active inside the actor code at any time. A turn consists of the complete execution of an actor method in response to a request from other actors or clients, or the complete execution of a timer/reminder callback. Even though these methods and callbacks are asynchronous, the Actors runtime does not interleave them. A turn must be fully finished before a new turn is allowed. In other words, an actor method or timer/reminder callback that is currently executing must be fully finished before a new call to a method or callback is allowed. A method or callback is considered to have finished if the execution has returned from the method or callback and the task returned by the method or callback has finished. It is worth emphasizing that turn-based concurrency is respected even across different methods, timers, and callbacks. The Actors runtime enforces turn-based concurrency by acquiring a per-actor lock at the beginning of a turn and releasing the lock at the end of the turn. Thus, turn-based concurrency is enforced on a per-actor basis and not across actors. Actor methods and timer/reminder callbacks can execute simultaneously on behalf of different actors.

This demo uses the turn-based model for invoking methods to implement an exclusive access to the the actor that governs the access to a shared resource.


# Architecture Design #
The following picture shows the architecture design of the test application.
<br/>
<br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/ServiceFabricLeaderElection/master/Images/Architecture.png)
<br/>

# Message Flow #
The following tasks try to acquire an exclusive lock on the same ResourceMutextActor with Id equal to "SharedResource":

- **TestStatefulService**: it's a reliable stateful service running in the same Service Fabric application. 
- **TestStatelessService**: it's a reliable stateless service running in the same Service Fabric application.
- **TestClient**: it's a console application playing the role of an external application that tries to acquire an exclusive lock on the resource protected by the actor by invoking its methods via the gateway service.

The Gateway Service is a stateless service running an ASP.NET Web API REST service hosted by OWIN that can be used by external applications to interact with ResourceMutexActor entities via HTTP. Each service tries to acquire a lock on the same ResourceMutexActor. When it acquires the lease, it becomes the leader and keeps renewing the lease on the actor for a configurable amount of steps. Then, the leader simulates a down or it explictly releases the lease on the actor. In any case, the leader will lose the lease, and another task will be able to acquire it. In fact, whn the leader keeps renewing the lease, the other services try to acquire the lease in a loop. Note: the implementation of the ResourceMutexActor class uses a reminder to check if the leader has renewed the lease on time, otherwise the reminder releases the lease.

# Code #
The following table show the code of the ResourceMutexActor class:


```CSharp
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
                    throw new ArgumentNullException(nameof(requesterId), 
						"requesterId argument cannot be null or empty.");
                }

                if (leaseInterval.TotalSeconds <= 0)
                {
                    throw new ArgumentException("leaseInterval cannot be less or equal to zero.", 
					nameof(leaseInterval));
                }

                var result = await StateManager.TryGetStateAsync<string>(LeaderIdState);
                DateTime now;
                if (result.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(result.Value))
                    {
                        // The resource is already acquired. 
						// Return true if requesterId == leaderId, false otherwise
                        if (string.Compare(requesterId, 
										   result.Value, 
											StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            // The acquire lease operation is idempotent. Add or update the leaseInterval and leaseDateTime states.
                            now = DateTime.UtcNow;
                            await StateManager.AddOrUpdateStateAsync(LeaseIntervalState, 
									leaseInterval, (k, v) => leaseInterval);
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

```
The following table contains the code executed by both the stateless and stateful services inside the RunAsync method to acquire, renew and release the lease on the ResourceMutexActor.

```CSharp
/// <summary>
/// This is the main entry point for your service instance.
/// </summary>
/// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
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
      var requesterId = $"{Context.ReplicaOrInstanceId}";

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
```

# Configuration Files #

**App.config** file in the **UserEmulator** project:

```xml
	<?xml version="1.0" encoding="utf-8" ?>
	<configuration>
	  <appSettings>
		<add key="gatewayUrl" value="http://localhost:9015/"/>
		<add key="stepCount" value="3" />
		<add key="acquireIntervaladd" value="10" />
		<add key="renewIntervaladd" value="10" />
		<add key="leaseIntervaladd" value="30" />
		<add key="downIntervaladd" value="45" />
	  </appSettings>
	  <startup> 
		<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2" />
	  </startup>
	</configuration>
```

**ApplicationParameters\Local.xml** file in the **PageViewTracer** project:

```xml
	<?xml version="1.0" encoding="utf-8"?>
	<Application xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
				 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
				 Name="fabric:/LeaderElection" 
				 xmlns="http://schemas.microsoft.com/2011/01/fabric">
	  <Parameters>
	    <Parameter Name="GatewayService_InstanceCount" Value="1" />
	    <Parameter Name="TestStatelessService_InstanceCount" Value="1" />
	    <Parameter Name="TestStatefulService_PartitionCount" Value="1" />
	    <Parameter Name="TestStatefulService_MinReplicaSetSize" Value="3" />
	    <Parameter Name="TestStatefulService_TargetReplicaSetSize" Value="3" />
	    <Parameter Name="ResourceMutexActorService_PartitionCount" Value="1" />
	    <Parameter Name="ResourceMutexActorService_MinReplicaSetSize" Value="3" />
	    <Parameter Name="ResourceMutexActorService_TargetReplicaSetSize" Value="3" />
	  </Parameters>
	</Application>
```

**ApplicationParameters\Cloud.xml** file in the **PageViewTracer** project:

```xml
	<?xml version="1.0" encoding="utf-8"?>
	<Application xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
				 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
				 Name="fabric:/LeaderElection" 
				 xmlns="http://schemas.microsoft.com/2011/01/fabric">
	  <Parameters>
	    <Parameter Name="GatewayService_InstanceCount" Value="1" />
	    <Parameter Name="TestStatelessService_InstanceCount" Value="1" />
	    <Parameter Name="TestStatefulService_PartitionCount" Value="1" />
	    <Parameter Name="TestStatefulService_MinReplicaSetSize" Value="3" />
	    <Parameter Name="TestStatefulService_TargetReplicaSetSize" Value="3" />
	    <Parameter Name="ResourceMutexActorService_PartitionCount" Value="1" />
	    <Parameter Name="ResourceMutexActorService_MinReplicaSetSize" Value="3" />
	    <Parameter Name="ResourceMutexActorService_TargetReplicaSetSize" Value="3" />
	  </Parameters>
	</Application>
```

**ApplicationManifest.xml** file in the **PageViewTracer** project:

```xml
    <?xml version="1.0" encoding="utf-8"?>
	<ApplicationManifest xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
						 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
						 ApplicationTypeName="LeaderElectionType" 
						 ApplicationTypeVersion="1.0.0" 
						 xmlns="http://schemas.microsoft.com/2011/01/fabric">
	  <Parameters>
	    <Parameter Name="GatewayService_InstanceCount" DefaultValue="-1" />
	    <Parameter Name="GatewayService_ResourceMutexActorServiceUri" DefaultValue="" />
	    <Parameter Name="TestStatelessService_InstanceCount" DefaultValue="-1" />
	    <Parameter Name="TestStatelessService_ResourceMutexActorServiceUri" DefaultValue="" />
	    <Parameter Name="TestStatelessService_StepCount" DefaultValue="3" />
	    <Parameter Name="TestStatelessService_AcquireIntervalParameter" DefaultValue="10" />
	    <Parameter Name="TestStatelessService_RenewIntervalParameter" DefaultValue="10" />
	    <Parameter Name="TestStatelessService_LeaseIntervalParameter" DefaultValue="30" />
	    <Parameter Name="TestStatelessService_DownIntervalParameter" DefaultValue="45" />
	    <Parameter Name="TestStatefulService_PartitionCount" DefaultValue="5" />
	    <Parameter Name="TestStatefulService_MinReplicaSetSize" DefaultValue="3" />
	    <Parameter Name="TestStatefulService_TargetReplicaSetSize" DefaultValue="3" />
	    <Parameter Name="TestStatefulService_ResourceMutexActorServiceUri" DefaultValue="" />
	    <Parameter Name="TestStatefulService_StepCount" DefaultValue="3" />
	    <Parameter Name="TestStatefulService_AcquireIntervalParameter" DefaultValue="10" />
	    <Parameter Name="TestStatefulService_RenewIntervalParameter" DefaultValue="10" />
	    <Parameter Name="TestStatefulService_LeaseIntervalParameter" DefaultValue="30" />
	    <Parameter Name="TestStatefulService_DownIntervalParameter" DefaultValue="45" />
	    <Parameter Name="ResourceMutexActorService_PartitionCount" DefaultValue="5" />
	    <Parameter Name="ResourceMutexActorService_MinReplicaSetSize" DefaultValue="3" />
	    <Parameter Name="ResourceMutexActorService_TargetReplicaSetSize" DefaultValue="3" />
	  </Parameters>
	  <!-- Import the ServiceManifest from the ServicePackage. 
		   The ServiceManifestName and ServiceManifestVersion 
	       should match the Name and Version attributes of the 
		   ServiceManifest element defined in the 
	       ServiceManifest.xml file. -->
	  <ServiceManifestImport>
	    <ServiceManifestRef ServiceManifestName="ResourceMutexActorServicePkg" ServiceManifestVersion="1.0.0" />
	    <ConfigOverrides />
	  </ServiceManifestImport>
	  <ServiceManifestImport>
	    <ServiceManifestRef ServiceManifestName="GatewayServicePkg" ServiceManifestVersion="1.0.0" />
	    <ConfigOverrides>
	      <ConfigOverride Name="Config">
	        <Settings>
	          <Section Name="ServiceConfig">
	            <Parameter Name="ResourceMutexActorServiceUri" 
						   Value="[GatewayService_ResourceMutexActorServiceUri]" />
	          </Section>
	        </Settings>
	      </ConfigOverride>
	    </ConfigOverrides>
	  </ServiceManifestImport>
	  <ServiceManifestImport>
	    <ServiceManifestRef ServiceManifestName="TestStatefulServicePkg" 
							ServiceManifestVersion="1.0.0" />
	    <ConfigOverrides>
	      <ConfigOverride Name="Config">
	        <Settings>
	          <Section Name="ServiceConfig">
	            <Parameter Name="ResourceMutexActorServiceUri" 
						   Value="[TestStatefulService_ResourceMutexActorServiceUri]" />
	            <Parameter Name="StepCount" 
						   Value="[TestStatefulService_StepCount]" />
	            <Parameter Name="AcquireIntervalParameter" 
						   Value="[TestStatefulService_AcquireIntervalParameter]" />
	            <Parameter Name="RenewIntervalParameter" 
						   Value="[TestStatefulService_RenewIntervalParameter]" />
	            <Parameter Name="LeaseIntervalParameter" 
						   Value="[TestStatefulService_LeaseIntervalParameter]" />
	            <Parameter Name="DownIntervalParameter" 
						   Value="[TestStatefulService_DownIntervalParameter]" />
	          </Section>
	        </Settings>
	      </ConfigOverride>
	    </ConfigOverrides>
	  </ServiceManifestImport>
	  <ServiceManifestImport>
	    <ServiceManifestRef ServiceManifestName="TestStatelessServicePkg" 
						    ServiceManifestVersion="1.0.0" />
	    <ConfigOverrides>
	      <ConfigOverride Name="Config">
	        <Settings>
	          <Section Name="ServiceConfig">
	            <Parameter Name="ResourceMutexActorServiceUri" 
						   Value="[TestStatelessService_ResourceMutexActorServiceUri]" />
	            <Parameter Name="StepCount" 
						   Value="[TestStatelessService_StepCount]" />
	            <Parameter Name="AcquireIntervalParameter" 
						   Value="[TestStatelessService_AcquireIntervalParameter]" />
	            <Parameter Name="RenewIntervalParameter" 
						   Value="[TestStatelessService_RenewIntervalParameter]" />
	            <Parameter Name="LeaseIntervalParameter" 
 						   Value="[TestStatelessService_LeaseIntervalParameter]" />
	            <Parameter Name="DownIntervalParameter" 
						   Value="[TestStatelessService_DownIntervalParameter]" />
	          </Section>
	        </Settings>
	      </ConfigOverride>
	    </ConfigOverrides>
	  </ServiceManifestImport>
	  <DefaultServices>
	    <!-- The section below creates instances of service types, when an instance of this 
	         application type is created. You can also create one or more instances 
			 of service type using the ServiceFabric PowerShell module.
	         The attribute ServiceTypeName below must match the name 
			 defined in the imported ServiceManifest.xml file. -->
	    <Service Name="GatewayService">
	      <StatelessService ServiceTypeName="GatewayServiceType" 
							InstanceCount="[GatewayService_InstanceCount]">
	        <SingletonPartition />
	      </StatelessService>
	    </Service>
	    <Service Name="TestStatefulService">
	      <StatefulService ServiceTypeName="TestStatefulServiceType" 
						   TargetReplicaSetSize="[TestStatefulService_TargetReplicaSetSize]"
						   MinReplicaSetSize="[TestStatefulService_MinReplicaSetSize]">
	        <UniformInt64Partition PartitionCount="[TestStatefulService_PartitionCount]" 
								   LowKey="-9223372036854775808" 
								   HighKey="9223372036854775807" />
	      </StatefulService>
	    </Service>
	    <Service Name="TestStatelessService">
	      <StatelessService ServiceTypeName="TestStatelessServiceType" 
							InstanceCount="[TestStatelessService_InstanceCount]">
	        <SingletonPartition />
	      </StatelessService>
	    </Service>
	    <Service Name="ResourceMutexActorService" 
				 GeneratedIdRef="a5b8e7b0-5a4b-4503-84ea-b6c490541180|Persisted">
	      <StatefulService ServiceTypeName="ResourceMutexActorServiceType" 
						   TargetReplicaSetSize="[ResourceMutexActorService_TargetReplicaSetSize]" 
						   MinReplicaSetSize="[ResourceMutexActorService_MinReplicaSetSize]">
	        <UniformInt64Partition PartitionCount="[ResourceMutexActorService_PartitionCount]" 
								   LowKey="-9223372036854775808" 
								   HighKey="9223372036854775807" />
	      </StatefulService>
	    </Service>
	  </DefaultServices>
	</ApplicationManifest>
```