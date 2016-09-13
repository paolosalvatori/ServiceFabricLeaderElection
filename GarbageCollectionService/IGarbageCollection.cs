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
using Microsoft.ServiceFabric.Services.Remoting; 
#endregion

namespace Microsoft.AzureCat.Samples.GarbageCollectionService
{
    public interface IGarbageCollection : IService
    {
        /// <summary>
        /// Schedule the delete of an Actor from the Actor service.
        /// </summary>
        /// <param name="serviceUri">Uri of the actor service to connect to.</param>
        /// <param name="actorId">ActorId of the actor to be deleted.</param>
        /// <returns></returns>
        Task DeleteActorAsync(Uri serviceUri, ActorId actorId);
    }
}