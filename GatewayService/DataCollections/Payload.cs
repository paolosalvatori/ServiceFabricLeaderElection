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
using Newtonsoft.Json;

#endregion

namespace Microsoft.AzureCat.Samples.GatewayService.DataCollections
{
    public class Payload
    {
        /// <summary>
        /// Gets or sets the resource id.
        /// </summary>
        [JsonProperty(PropertyName = "resourceId", Order = 1)]
        public string ResourceId { get; set; }

        /// <summary>
        /// Gets or sets the requester id.
        /// </summary>
        [JsonProperty(PropertyName = "requesterId", Order = 2)]
        public string RequesterId { get; set; }

        /// <summary>
        /// Gets or sets the lease time interval.
        /// </summary>
        [JsonProperty(PropertyName = "leaseInterval", Order = 3)]
        public TimeSpan LeaseInterval { get; set; }
    }
}
