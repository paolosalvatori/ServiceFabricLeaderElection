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
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

#endregion

namespace Microsoft.AzureCat.Samples.TestClient
{
    internal class Program
    {
        #region Private Constants

        //************************************
        // Constants
        //************************************
        private const string ResourceId = "SharedResource";

        //***************************
        // Configuration Parameters
        //***************************
        private const string GatewayUrlParameter = "gatewayUrl";
        private const string StepCountParameter = "stepCount";
        private const string AcquireIntervalParameter = "acquireInterval";
        private const string RenewIntervalParameter = "renewInterval";
        private const string LeaseIntervalParameter = "leaseInterval";
        private const string DownIntervalParameter = "downDelay";

        //************************************
        // Default Values
        //************************************
        private const int DefaultStepCountParameter = 5;
        private const int DefaultAcquireIntervalInSeconds = 10;
        private const int DefaultRenewIntervalInSeconds = 10;
        private const int DefaultLeaseIntervalInSeconds = 30;
        private const int DefaultDownIntervalInSeconds = 45;

        //************************************
        // Default Values
        //************************************
        private const string DefaultGatewayUrl = "http://localhost:8082/worker";

        #endregion

        #region Private Static Fields

        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = CancellationTokenSource.Token;

        private static readonly string Line = new string('-', 129);

        /// <summary>
        /// Gets or Sets the gateway URL
        /// </summary>
        private static string gatewayUrl;

        /// <summary>
        /// Gets or Sets the count of the steps after which a leader simulates a down or releases the mutex
        /// </summary>
        private static int stepCount;

        /// <summary>
        /// Gets or Sets the acquire interval
        /// </summary>
        private static TimeSpan acquireInterval;

        /// <summary>
        /// Gets or Sets the renew interval
        /// </summary>
        private static TimeSpan renewInterval;

        /// <summary>
        /// Gets or Sets the lease interval
        /// </summary>
        private static TimeSpan leaseInterval;

        /// <summary>
        /// Gets or Sets the time period for which the instance delays to simulate a down
        /// </summary>
        private static TimeSpan downInterval;

        private static readonly List<Test> TestList = new List<Test>
        {
            new Test
            {
                Name = "Compete for the Resource",
                Description = "Simulates an external process that competes for the ownership of the resource.",
                Action = TestMethod
            }
        };

        #endregion

        #region Main Method
        public static void Main(string[] args)
        {
            try
            {
                // Sets window size and cursor color
                Console.SetWindowSize(130, 30);
                Console.ForegroundColor = ConsoleColor.White;

                // Reads configuration settings
                ReadConfiguration();

                int i;
                while ((i = SelectOption()) != TestList.Count + 1)
                {
                    try
                    {
                        PrintTestParameters(TestList[i - 1].Name);
                        TestList[i - 1].Action();
                    }
                    catch (Exception ex)
                    {
                        PrintException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
        }
        #endregion


        #region Test Methods

        private static void TestMethod()
        {
            try
            {
                
                Task.Run(async () =>
                 {
                     // Creates http proxy
                     var httpClient = new HttpClient
                     {
                         BaseAddress = new Uri(gatewayUrl)
                     };

                     httpClient.DefaultRequestHeaders.Add("ContentType", "application/json");
                     httpClient.DefaultRequestHeaders.Accept.Clear();
                     httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                     Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] HttpClient for the [{ResourceId}] created.");

                     // Create the requesterId
                     var requesterId = "TestConsoleApp";

                     while (true)
                     {
                         CancellationToken.ThrowIfCancellationRequested();

                         

                         var json = JsonConvert.SerializeObject(
                             new Payload
                             {
                                 ResourceId = ResourceId,
                                 RequesterId = requesterId,
                                 LeaseInterval = leaseInterval
                             });

                         var postContent = new StringContent(json, Encoding.UTF8, "application/json");
                         var response = await httpClient.PostAsync(Combine(httpClient.BaseAddress.AbsoluteUri, "api/gateway/acquirelease"), postContent, CancellationToken);
                         response.EnsureSuccessStatusCode();
                         var returnValue = await response.Content.ReadAsStringAsync();
                         bool ok;
                         bool.TryParse(returnValue, out ok);

                         if (ok)
                         {
                             Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester acquired a lease on [{ResourceId}] acquired. StepCount=[{stepCount}]");
                             for (var i = 0; i < stepCount; i++)
                             {
                                 var step = i + 1;
                                 Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester is waiting [{renewInterval.Seconds}] seconds before renewing the lease on [{ResourceId}]. Step [{step}] of [{stepCount}]...");
                                // Wait for time period equal to renewInterval parameter
                                Task.Delay(renewInterval, CancellationToken).Wait(CancellationToken);

                                // Renew the lease
                                Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester renewing the lease on [{ResourceId}]. Step [{step}] of [{stepCount}]...");
                                 postContent = new StringContent(json, Encoding.UTF8, "application/json");
                                 response = await httpClient.PostAsync(Combine(httpClient.BaseAddress.AbsoluteUri, "api/gateway/renewlease"), postContent, CancellationToken);
                                 response.EnsureSuccessStatusCode();
                                 Console.WriteLine(
                                     $" - [{DateTime.Now.ToLocalTime()}] Requester successfully renewed the lease on [{ResourceId}]. Step [{step}] of [{stepCount}].");
                             }

                            // Simulate a down or mutex release
                            var random = new Random();
                             var value = random.Next(1, 3);
                             if (value == 1)
                             {
                                // Simulate a down period
                                Console.WriteLine(
                                     $" - [{DateTime.Now.ToLocalTime()}] Requester simulating a down of [{downInterval.Seconds}] seconds...");
                                 Task.Delay(downInterval, CancellationToken).Wait(CancellationToken);
                             }
                             else
                             {
                                // Release the mutex lease
                                Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester releasing the lease on [{ResourceId}]...");
                                 postContent = new StringContent(json, Encoding.UTF8, "application/json");
                                 response = await httpClient.PostAsync(Combine(httpClient.BaseAddress.AbsoluteUri, "api/gateway/releaselease"), postContent, CancellationToken);
                                 response.EnsureSuccessStatusCode();
                                 Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester successfully released the lease on [{ResourceId}]");
                             }
                         }

                        // Wait before retrying to acquire the lease
                        Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Requester is waiting [{acquireInterval.Seconds}] seconds before retrying to acquire a lease on [{ResourceId}]...");
                        await Task.Delay(acquireInterval, CancellationToken);
                     }
                    // ReSharper disable once FunctionNeverReturns
                }, CancellationToken).Wait();
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
        }
        #endregion

        #region Private Static Methods

        private static int SelectOption()
        {
            // Create a line

            int optionCount = TestList.Count + 1;

            Console.WriteLine("Select an option:");
            Console.WriteLine(Line);

            for (int i = 0; i < TestList.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[{0}] ", i + 1);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(TestList[i].Name);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" - " + TestList[i].Description);
            }

            // Add exit option
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[{0}] ", optionCount);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Exit");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" - Close the test application.");
            Console.WriteLine(Line);

            // Select an option
            Console.WriteLine($"Press a key between [1] and [{optionCount}]: ");
            char key = 'a';
            while (key < '1' || key > ('1' + optionCount))
            {
                key = Console.ReadKey(true).KeyChar;
            }
            return key - '1' + 1;
        }

        private static void PrintException(
            Exception ex,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            // Write Line
            Console.WriteLine(Line);

            InternalPrintException(ex, sourceFilePath, memberName, sourceLineNumber);

            // Write Line
            Console.WriteLine(Line);
        }

        private static void InternalPrintException(
            Exception ex,
            string sourceFilePath = "",
            string memberName = "",
            int sourceLineNumber = 0)
        {
            AggregateException exception = ex as AggregateException;
            if (exception != null)
            {
                foreach (Exception e in exception.InnerExceptions)
                {
                    if (sourceFilePath != null)
                    {
                        InternalPrintException(e, sourceFilePath, memberName, sourceLineNumber);
                    }
                }
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{ex.GetType().Name}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.Yellow;
            string fileName = null;
            if (File.Exists(sourceFilePath))
            {
                FileInfo file = new FileInfo(sourceFilePath);
                fileName = file.Name;
            }
            Console.Write(string.IsNullOrWhiteSpace(fileName) ? "Unknown" : fileName);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(string.IsNullOrWhiteSpace(memberName) ? "Unknown" : memberName);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(sourceLineNumber.ToString(CultureInfo.InvariantCulture));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(": ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(!string.IsNullOrWhiteSpace(ex.Message) ? ex.Message : "An error occurred.");
        }

       private static void PrintTestParameters(string testName)
        {
            Console.WriteLine(Line);
            Console.WriteLine($" - [{DateTime.Now.ToLocalTime()}] Test [{testName}] parameters:");
            Console.WriteLine(Line);
            Console.WriteLine($" - Gateway URL: [{gatewayUrl}]");
            Console.WriteLine($" - StepCount: [{stepCount}]");
            Console.WriteLine($" - AcquireInterval: [{acquireInterval}]");
            Console.WriteLine($" - RenewInterval: [{renewInterval}]");
            Console.WriteLine($" - LeaseInterval: [{leaseInterval}]");
            Console.WriteLine($" - DownInterval: [{downInterval}]");
            Console.WriteLine(Line);
        }

        
        private static void ReadConfiguration()
        {
            try
            {
                gatewayUrl = ConfigurationManager.AppSettings[GatewayUrlParameter] ?? DefaultGatewayUrl;
                if (string.IsNullOrWhiteSpace(gatewayUrl))
                {
                    throw new ArgumentException($"The [{GatewayUrlParameter}] setting in the configuration file is null or invalid.");
                }

                int value;
                var parameter = ConfigurationManager.AppSettings[StepCountParameter];
                if (!string.IsNullOrWhiteSpace(parameter) && int.TryParse(parameter, out value))
                {
                    stepCount = value;
                }
                else
                {
                    stepCount = DefaultStepCountParameter;
                }

                parameter = ConfigurationManager.AppSettings[AcquireIntervalParameter];
                if (!string.IsNullOrWhiteSpace(parameter) && int.TryParse(parameter, out value))
                {
                    acquireInterval = TimeSpan.FromSeconds(value);
                }
                else
                {
                    acquireInterval = TimeSpan.FromSeconds(DefaultAcquireIntervalInSeconds);
                }

                parameter = ConfigurationManager.AppSettings[RenewIntervalParameter];
                if (!string.IsNullOrWhiteSpace(parameter) && int.TryParse(parameter, out value))
                {
                    renewInterval = TimeSpan.FromSeconds(value);
                }
                else
                {
                    renewInterval = TimeSpan.FromSeconds(DefaultRenewIntervalInSeconds);
                }

                parameter = ConfigurationManager.AppSettings[LeaseIntervalParameter];
                if (!string.IsNullOrWhiteSpace(parameter) && int.TryParse(parameter, out value))
                {
                    leaseInterval = TimeSpan.FromSeconds(value);
                }
                else
                {
                    leaseInterval = TimeSpan.FromSeconds(DefaultLeaseIntervalInSeconds);
                }

                parameter = ConfigurationManager.AppSettings[DownIntervalParameter];
                if (!string.IsNullOrWhiteSpace(parameter) && int.TryParse(parameter, out value))
                {
                    downInterval = TimeSpan.FromSeconds(value);
                }
                else
                {
                    downInterval = TimeSpan.FromSeconds(DefaultDownIntervalInSeconds);
                }
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
        }

        public static string Combine(string uri1, string uri2)
        {
            uri1 = uri1.TrimEnd('/');
            uri2 = uri2.TrimStart('/');
            return $"{uri1}/{uri2}";
        }

        #endregion
    }

    internal class Test
    {
        #region Public Properties

        public string Name { get; set; }

        public string Description { get; set; }

        public Action Action { get; set; }

        #endregion
    }

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