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
The following picture shows the architecture design of the application.
<br/>
<br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/Architecture.png)
<br/>

# Message Flow #
1. A Windows Forms application is used to emulate a configurable amount of users sending events to the ingestion pipeline of the click analytics system.</br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/Client.png)
<br/>
The client application uses a separate Task to emulate each user. Each user session is composed by a series of JSON messages sent to the service endpoint of the click analytics ingestion pipeline:
	- a special	session start event
	- a configurable amount of user events (click, mouse move, enter text)
	- a special session stop event
2. The **PageViewtWebService** stateless service receives requests using the **POST** method. The body of the request is in **JSON** format, the **Content-Type** header is equal to application/json, while the custom userId header contains the user id. The payload contains the **userId** (cross check) and the **User Event**. The service writes events into an **Event Hub**. The **userId** is used as a value for the [EventData.PartitionKey](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.eventdata.partitionkey.aspx) property. The **userid** is also stored in a custom **userId** property, while the **eventType** (start session, user event, stop session) is stored in another custom property of the [EventData](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.eventdata.aspx) message. Note: this microservice uses a pool of [EventHubClient](https://msdn.microsoft.com/library/azure/microsoft.servicebus.messaging.eventhubclient.aspx) objects to increase the throughput of the ingestion pipeline.
3. The **EventProcessorHostService** uses an **EventProcessorHost** listener to receive messages from the **Event Hub**.
4. The **EventProcessorHostService** retrieves the **userId** and **eventType** from the [Properties](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.eventdata.properties.aspx) collection of the [EventData](https://msdn.microsoft.com/en-us/library/microsoft.servicebus.messaging.eventdata.aspx) message and the payload from the message body, and uses a [CloudAppendBlob](https://msdn.microsoft.com/en-us/library/microsoft.windowsazure.storage.blob.cloudappendblob.aspx) object to write the event to a [Append Blob](https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/) inside a given storage container. <br/> The name is of the blob is **{userId}_{session_start_timestamp}.log**. <br/><br/>![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/Blobs.png)<br/>
<br/> When the it receives a stop session event, the microservice sends a **JSON** message to **Service Bus Queue**. The message contains the  **userId** and **uri** of the [Append Blob](https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/) containing the events of the user visit. The message is received and processed by an external hot path analytics system. You can use the [Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer) to read messages from the **Service Bus Queue**, as shown in the following picture. <br/><br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/QueueMessage.png)
<br/><br/>To monitor the message flow in real-time, you can create a test **Consumer Group** other than the one used by the application, and use the aaaaaaaa to create and run a **Consumer Group Listener**, as shown in the following picture.<br/><br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/EventHub.png)
<br/><br/>Each **Append Blob** contains all the user events in **JSON** format tracked during the user session: <br/><br/>
![alt tag](https://raw.githubusercontent.com/paolosalvatori/servicefabricclickanalytics/master/Images/BlobContent.png)
<br/>

# Service Fabric Application #
The Service Fabric application ingest events from the input Event Hub, processes sensor readings and generates an alert whenever a value outside of the tolerance range is received. The application is composed of three services:

- **PageViewWebService**: this is a stateless service hosting **OWIN** and exposing a REST ingestion service. The service has been implemented using an **ASP.NET Web API** REST service. The service is implemented as an **ApiController** that exposes a **POST** method invoked by client-side scripts. The service uses a pool of **EventHubClient** objects to increase the performance. Each **EventHubClient** object is cached in a static list and uses an **AMQP** session to send events into the **Event Hub**.
- **EventProcessorHostService**: this is a stateless service that creates an **EventProcessorHost** listener to receive messages from the event hub. Note: to maximize the throughput, make sure that the number of service instances and cluster nodes matches the number of event hub partitions. The **ProcessEventsAsync** method of the **EventProcessor** class creates and caches a [CloudAppendBlob](https://msdn.microsoft.com/en-us/library/microsoft.windowsazure.storage.blob.cloudappendblob.aspx) object to write the event to a [Append Blob](https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/) object, for each user session, to write page views to append blobs.

**Note**: one of the advantages of stateless services over stateful services is that by specifying **InstanceCount="-1"** in the **ApplicationManifest.xml**, you can create an instance of the service on each node of the Service Fabric cluster. When the cluster uses [Virtual Machine Scale Sets](https://azure.microsoft.com/en-gb/documentation/articles/virtual-machines-vmss-overview/) to to scale up and down the number of cluster nodes, this allows to automatically scale up and scale down the number of instances of a stateless service based on the autoscaling rules and traffic conditions.

# Recommendations #
In order to maximize the throughput of the demo, put in practice the following recommendations:

- Deploy the **Service Fabric** application on a cluster with at least 16 nodes
- Make sure to create an **Event Hub** with a sufficient number of partitions (for example, 16) in a dedicated Service Bus namespace with no other **Event Hubs** and increase the number to **Throughput Units** to be equal to the number of partitions of the **Event Hub**, at least for the duration of the load tests
- Modify the code to write **Append Blobs** to a pool of **Storage Accounts** instead of a single **Storage Account** as in the current implementation
- Deploy the **Service Fabric** services with the **InstanceCount** attribute equal to -1
- Repeat the test when the **Service Fabric** cluster will support **Virtual Machines Scale Sets**
- Make sure to properly configure **Visual Studio Load Test** to generate enough traffic against the Azure-hosted application. Consider using multiple instances of the Load Test running on multiple Azure subscriptions.

# Application Configuration #
Make sure to replace the following placeholders in the project files below before deploying and testing the application on the local development Service Fabric cluster or before deploying the application to your Service Fabric cluster on Microsoft Azure.

## Placeholders ##
This list contains the placeholders that need to be replaced before deploying and running the application:

- **[ServiceBusConnectionString]**: defines the connection string of the **Service Bus** namespace that contains the **Event Hub** and **Queue** used by the solution.
- **[StorageAccountConnectionString]**: contains the connection string of the **Storage Account** used by the **EventProcessorHost** to store partition lease information when reading data from the input **Event Hub**.
- **[EventHubName]**: contains the name of the input **Event Hub**.
- **[ConsumerGroupName]**: contains the name of the **Consumer Group** used by the **EventProcessorHost** to read data from the input **Event Hub**.
- **[ContainerName]**: defines the name of the **Storage Container** where the **EventProcessorHost** writes **Append Blobs**.
- **[QueueName]**: contains the name of the **Service Bus Queue** used by the **EventProcessorHost** send a message to the external hot path analytics system when user session completes.
- **[CheckpointCount]**: this number defines after how many messages the **EventProcessorHost** invokes the **ChechpointAsync** method.
- **[EventHubClientNumber]**: this number defines how many [EventHubClient](https://msdn.microsoft.com/library/azure/microsoft.servicebus.messaging.eventhubclient.aspx) objects are contained in the connection pool of the **PageViewWebService**.

## Configuration Files ##

**App.config** file in the **UserEmulator** project:

```xml    
    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
		<appSettings>
			<add key="url" value="http://localhost:8085/usersessions;
                                  http://[NAME].[REGION].cloudapp.azure.com:8085/usersessions"/>
			<add key="userCount" value="20"/>
			<add key="eventInterval" value="2000"/>
			<add key="eventsPerUserSession" value="50"/>
		</appSettings>
		<startup>
			<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5"/>
		</startup>
		<runtime>
			<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
				<dependentAssembly>
					<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral"/>
					<bindingRedirect oldVersion="0.0.0.0-8.0.0.0" newVersion="8.0.0.0"/>
				</dependentAssembly>
			</assemblyBinding>
		</runtime>
    </configuration>
```

**ApplicationParameters\Local.xml** file in the **PageViewTracer** project:

```xml
	<?xml version="1.0" encoding="utf-8"?>
	<Application xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
                 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
                 Name="fabric:/PageViewTracer" 
                 xmlns="http://schemas.microsoft.com/2011/01/fabric">
		<Parameters>
			<Parameter Name="EventProcessorHostService_InstanceCount" 
                       Value="1" />
			<Parameter Name="EventProcessorHostService_StorageAccountConnectionString" 
                       Value="[StorageAccountConnectionString]" />
			<Parameter Name="EventProcessorHostService_ServiceBusConnectionString" 
                       Value="[ServiceBusConnectionString]" />
			<Parameter Name="EventProcessorHostService_EventHubName" 
                       Value="[EventHubName]" />
			<Parameter Name="EventProcessorHostService_ConsumerGroupName" 
                       Value="[ConsumerGroupName]" />
			<Parameter Name="EventProcessorHostService_ContainerName" 
                       Value="[ContainerName]" />
			<Parameter Name="EventProcessorHostService_QueueName" 
                       Value="[QueueName]" />
			<Parameter Name="EventProcessorHostService_MaxRetryCount" 
                       Value="3" />
			<Parameter Name="EventProcessorHostService_CheckpointCount" 
                       Value="[CheckpointCount]" />
			<Parameter Name="EventProcessorHostService_BackoffDelay" 
                       Value="1" />
			<Parameter Name="PageViewWebService_InstanceCount" 
                       Value="1" />
			<Parameter Name="PageViewWebService_ServiceBusConnectionString" 
                       Value="[ServiceBusConnectionString]" />
			<Parameter Name="PageViewWebService_EventHubName" 
                       Value="[EventHubName]" />
			<Parameter Name="PageViewWebService_EventHubClientNumber" 
                       Value="[EventHubClientNumber]" />
			<Parameter Name="PageViewWebService_MaxRetryCount" 
                       Value="3" />
			<Parameter Name="PageViewWebService_BackoffDelay" 
                       Value="1" />
		</Parameters>
	</Application>
```

**ApplicationParameters\Cloud.xml** file in the **PageViewTracer** project:

```xml
	<?xml version="1.0" encoding="utf-8"?>
	<Application xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
                 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
                 Name="fabric:/PageViewTracer" 
                 xmlns="http://schemas.microsoft.com/2011/01/fabric">
		<Parameters>
			<Parameter Name="EventProcessorHostService_InstanceCount" 
                       Value="-1" />
			<Parameter Name="EventProcessorHostService_StorageAccountConnectionString" 
                       Value="[StorageAccountConnectionString]" />
			<Parameter Name="EventProcessorHostService_ServiceBusConnectionString" 
                       Value="[ServiceBusConnectionString]" />
			<Parameter Name="EventProcessorHostService_EventHubName" 
                       Value="[EventHubName]" />
			<Parameter Name="EventProcessorHostService_ConsumerGroupName" 
                       Value="[ConsumerGroupName]" />
			<Parameter Name="EventProcessorHostService_ContainerName" 
                       Value="[ContainerName]" />
			<Parameter Name="EventProcessorHostService_QueueName" 
                       Value="[QueueName]" />
			<Parameter Name="EventProcessorHostService_MaxRetryCount" 
                       Value="3" />
			<Parameter Name="EventProcessorHostService_CheckpointCount" 
                       Value="[CheckpointCount]" />
			<Parameter Name="EventProcessorHostService_BackoffDelay" 
                       Value="1" />
			<Parameter Name="PageViewWebService_InstanceCount" 
                       Value="-1" />
			<Parameter Name="PageViewWebService_ServiceBusConnectionString" 
                       Value="[ServiceBusConnectionString]" />
			<Parameter Name="PageViewWebService_EventHubName" 
                       Value="[EventHubName]" />
			<Parameter Name="PageViewWebService_EventHubClientNumber" 
                       Value="[EventHubClientNumber]" />
			<Parameter Name="PageViewWebService_MaxRetryCount" 
                       Value="3" />
			<Parameter Name="PageViewWebService_BackoffDelay" 
                       Value="1" />
		</Parameters>
	</Application>
```

**ApplicationManifest.xml** file in the **PageViewTracer** project:

```xml
    <?xml version="1.0" encoding="utf-8"?>
    <ApplicationManifest xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
                         ApplicationTypeName="PageViewTracerType" 
                         ApplicationTypeVersion="1.0.1" 
                         xmlns="http://schemas.microsoft.com/2011/01/fabric">
		<Parameters>
	      <Parameter Name="EventProcessorHostService_InstanceCount" DefaultValue="-1" />
	      <Parameter Name="EventProcessorHostService_StorageAccountConnectionString" DefaultValue="" />
	      <Parameter Name="EventProcessorHostService_ServiceBusConnectionString" DefaultValue="" />
	      <Parameter Name="EventProcessorHostService_EventHubName" DefaultValue="" />
	      <Parameter Name="EventProcessorHostService_ConsumerGroupName" DefaultValue="" />
	      <Parameter Name="EventProcessorHostService_ContainerName" DefaultValue="usersessions" />
	      <Parameter Name="EventProcessorHostService_QueueName" DefaultValue="usersessions" />
	      <Parameter Name="EventProcessorHostService_MaxRetryCount" DefaultValue="3" />
	      <Parameter Name="EventProcessorHostService_CheckpointCount" DefaultValue="100" />
	      <Parameter Name="EventProcessorHostService_BackoffDelay" DefaultValue="1" />
	      <Parameter Name="PageViewWebService_InstanceCount" DefaultValue="-1" />
	      <Parameter Name="PageViewWebService_ServiceBusConnectionString" DefaultValue="" />
	      <Parameter Name="PageViewWebService_EventHubName" DefaultValue="" />
	      <Parameter Name="PageViewWebService_EventHubClientNumber" DefaultValue="32" />
	      <Parameter Name="PageViewWebService_MaxRetryCount" DefaultValue="3" />
	      <Parameter Name="PageViewWebService_BackoffDelay" DefaultValue="1" />
		</Parameters>
		<ServiceManifestImport>
      		<ServiceManifestRef ServiceManifestName="EventProcessorHostServicePkg" 
                                ServiceManifestVersion="1.0.0" />
			<ConfigOverrides>
			<ConfigOverride Name="Config">
				    <Settings>
						<Section Name="EventProcessorHostConfig">
							<Parameter Name="StorageAccountConnectionString" 
                                       Value="[EventProcessorHostService_StorageAccountConnectionString]" />
							<Parameter Name="ServiceBusConnectionString" 
                                       Value="[EventProcessorHostService_ServiceBusConnectionString]" />
							<Parameter Name="EventHubName" 
                                       Value="[EventProcessorHostService_EventHubName]" />
							<Parameter Name="ConsumerGroupName" 
                                       Value="[EventProcessorHostService_ConsumerGroupName]" />
							<Parameter Name="ContainerName" 
                                       Value="[EventProcessorHostService_ContainerName]" />
							<Parameter Name="QueueName" 
                                       Value="[EventProcessorHostService_QueueName]" />
							<Parameter Name="CheckpointCount" 
                                       Value="[EventProcessorHostService_CheckpointCount]" />
							<Parameter Name="MaxRetryCount" 
                                       Value="[EventProcessorHostService_MaxRetryCount]" />
							<Parameter Name="BackoffDelay" 
                                       Value="[EventProcessorHostService_BackoffDelay]" />
						</Section>
				    </Settings>
				</ConfigOverride>
			</ConfigOverrides>
		</ServiceManifestImport>
		<ServiceManifestImport>
			<ServiceManifestRef ServiceManifestName="PageViewWebServicePkg" 
                                ServiceManifestVersion="1.0.1" />
			<ConfigOverrides>
				<ConfigOverride Name="Config">
					<Settings>
						<Section Name="PageViewWebServiceConfig">
							<Parameter Name="ServiceBusConnectionString" 
                                       Value="[PageViewWebService_ServiceBusConnectionString]" />
							<Parameter Name="EventHubName" 
                                       Value="[PageViewWebService_EventHubName]" />
							<Parameter Name="EventHubClientNumber" 
                                       Value="[PageViewWebService_EventHubClientNumber]" />
							<Parameter Name="MaxRetryCount" 
                                       Value="[PageViewWebService_MaxRetryCount]" />
							<Parameter Name="BackoffDelay" 
                                       Value="[PageViewWebService_BackoffDelay]" />
						</Section>
					</Settings>
				</ConfigOverride>
			</ConfigOverrides>
		</ServiceManifestImport>
		<DefaultServices>
			<Service Name="EventProcessorHostService">
				<StatelessService ServiceTypeName="EventProcessorHostServiceType" 
                                  InstanceCount="[EventProcessorHostService_InstanceCount]">
					<SingletonPartition />
				</StatelessService>
			</Service>
			<Service Name="PageViewWebService">
				<StatelessService ServiceTypeName="PageViewWebServiceType" 
                                  InstanceCount="[PageViewWebService_InstanceCount]">
				<SingletonPartition />
			</StatelessService>
			</Service>
		</DefaultServices>
	</ApplicationManifest>
```