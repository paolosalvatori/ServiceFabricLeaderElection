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

#region MyRegion
using System;
using System.Fabric;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Owin; 
#endregion

namespace Microsoft.AzureCat.Samples.GatewayService
{
    internal class OwinCommunicationListener : ICommunicationListener
    {
        #region Private Fields
        private readonly ServiceEventSource eventSource;
        private readonly Action<IAppBuilder> startup;
        private readonly ServiceContext serviceContext;
        private readonly string endpointName;
        private readonly string appRoot;

        private IDisposable webApp;
        private string publishAddress;
        private string listeningAddress;
        #endregion

        #region Public Constructors
        public OwinCommunicationListener(Action<IAppBuilder> startup, ServiceContext serviceContext, ServiceEventSource eventSource, string endpointName)
            : this(startup, serviceContext, eventSource, endpointName, null)
        {
        }

        public OwinCommunicationListener(Action<IAppBuilder> startup, ServiceContext serviceContext, ServiceEventSource eventSource, string endpointName, string appRoot)
        {
            if (startup == null)
            {
                throw new ArgumentNullException(nameof(startup));
            }

            if (serviceContext == null)
            {
                throw new ArgumentNullException(nameof(serviceContext));
            }

            if (endpointName == null)
            {
                throw new ArgumentNullException(nameof(endpointName));
            }

            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            this.startup = startup;
            this.serviceContext = serviceContext;
            this.endpointName = endpointName;
            this.eventSource = eventSource;
            this.appRoot = appRoot;
        }
        #endregion

        #region Public Properties
        public bool ListenOnSecondary { get; set; } 
        #endregion

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var serviceEndpoint = serviceContext.CodePackageActivationContext.GetEndpoint(endpointName);
            var port = serviceEndpoint.Port;

            if (serviceContext is StatefulServiceContext)
            {
                var statefulServiceContext = serviceContext as StatefulServiceContext;

                listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "http://+:{0}/{1}{2}/{3}/{4}",
                    port,
                    string.IsNullOrWhiteSpace(appRoot)
                        ? string.Empty
                        : appRoot.TrimEnd('/') + '/',
                    statefulServiceContext.PartitionId,
                    statefulServiceContext.ReplicaId,
                    Guid.NewGuid());
            }
            else if (serviceContext is StatelessServiceContext)
            {
                listeningAddress = string.Format(
                    CultureInfo.InvariantCulture,
                    "http://+:{0}/{1}",
                    port,
                    string.IsNullOrWhiteSpace(appRoot)
                        ? string.Empty
                        : appRoot.TrimEnd('/') + '/');
            }
            else
            {
                throw new InvalidOperationException();
            }

            publishAddress = listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);

            try
            {
                eventSource.Message($"Starting web server on [{listeningAddress}]");
                webApp = WebApp.Start(listeningAddress, appBuilder => startup.Invoke(appBuilder));
                eventSource.ServiceMessage(serviceContext, "Listening on " + publishAddress);
                return Task.FromResult(publishAddress);
            }
            catch (Exception ex)
            {
                eventSource.ServiceMessage(serviceContext, "Web server failed to open: " + ex);
                StopWebServer();
                throw;
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            eventSource.ServiceMessage(serviceContext, "Closing web server");
            StopWebServer();
            return Task.FromResult(true);
        }

        public void Abort()
        {
            eventSource.ServiceMessage(serviceContext, "Aborting web server");
            StopWebServer();
        }

        private void StopWebServer()
        {
            if (webApp == null)
            {
                return;
            }
            try
            {
                webApp.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // no-op
            }
        }
    }
}
