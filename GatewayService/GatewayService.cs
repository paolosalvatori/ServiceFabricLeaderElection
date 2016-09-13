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
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime; 

#endregion

namespace Microsoft.AzureCat.Samples.GatewayService
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class GatewayService : StatelessService
    {
        #region Private Constants

        //************************************
        // Parameters
        //************************************
        private const string ConfigurationPackage = "Config";
        private const string ConfigurationSection = "ServiceConfig";
        private const string ResourceMutexActorServiceUriParameter = "ResourceMutexActorServiceUri";

        #endregion

        #region Public Static Properties
        /// <summary>
        /// Gets or Sets the Uri of the ResourceMutextActorService
        /// </summary>
        public static string ResourceMutextActorServiceUri { get; private set; }

        #endregion

        public GatewayService(StatelessServiceContext context)
            : base(context)
        {
            try
            {
                // Read settings from the ServiceConfig section in the Settings.xml file
                var activationContext = context.CodePackageActivationContext;
                var config = activationContext.GetConfigurationPackageObject(ConfigurationPackage);
                var section = config.Settings.Sections[ConfigurationSection];

                // Check if a parameter called ResourceMutexActorServiceUriParameter exists in the ServiceConfig config section
                if (section.Parameters.Any(p => string.Compare(p.Name,
                                                               ResourceMutexActorServiceUriParameter,
                                                               StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    var parameter = section.Parameters[ResourceMutexActorServiceUriParameter];
                    ResourceMutextActorServiceUri = !string.IsNullOrWhiteSpace(parameter?.Value)
                                                    ? parameter.Value
                                                    :
                                                    // By default, the current service assumes that if no URI is explicitly defined for the actor service
                                                    // in the Setting.xml file, the latter is hosted in the same Service Fabric application.
                                                    $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                }
                else
                {
                    // By default, the current service assumes that if no URI is explicitly defined for the actor service
                    // in the Setting.xml file, the latter is hosted in the same Service Fabric application.
                    ResourceMutextActorServiceUri = $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                }
                ServiceEventSource.Current.Message($"ResourceMutextActorServiceUri=[{ResourceMutextActorServiceUri}]");
            }
            catch (KeyNotFoundException)
            {
                ResourceMutextActorServiceUri = $"fabric:/{context.ServiceName.Segments[1]}ResourceMutexActorService";
                ServiceEventSource.Current.Message("KeyNotFoundException! Using default values for parameters");
                ServiceEventSource.Current.Message($"ResourceMutextActorServiceUri=[{ResourceMutextActorServiceUri}]");
            }
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[]
            {
                new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, ServiceEventSource.Current, "ServiceEndpoint"))
            };
        }
    }
}
