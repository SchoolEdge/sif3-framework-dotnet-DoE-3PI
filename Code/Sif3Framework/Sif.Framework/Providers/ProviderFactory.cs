﻿using log4net;
using Sif.Framework.Model;
using Sif.Framework.Model.DataModels;
using Sif.Framework.Model.Settings;
using Sif.Framework.Providers;
using Sif.Framework.Service;
using Sif.Framework.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sif.Framework.Providers
{
    public class ProviderFactory
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Object locked = new Object();

        private static ProviderFactory factory = null;

        // Active Providers for event publishing. These providers run in the background as an independent thread.
        private Dictionary<ModelObjectInfo, IService> providers = new Dictionary<ModelObjectInfo, IService>();

        private Dictionary<ModelObjectInfo, Thread> providerThreads = new Dictionary<ModelObjectInfo, Thread>();

        // Known providers that can be instantiated for standard request/response
        private Dictionary<ModelObjectInfo, ProviderClassInfo> providerClasses = new Dictionary<ModelObjectInfo, ProviderClassInfo>();

        public static ProviderFactory createFactory()
        {
            lock (locked)
            {
                log.Debug("Total Threads running before initialising provider Factory: " + Process.GetCurrentProcess().Threads.Count + " threads.");
                if (factory == null)
                {
                    try
                    {
                        factory = new ProviderFactory();
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to initialise provider factory. Provider won't run.", ex);
                        factory = null;
                    }
                }
                log.Debug("Total Threads running after initialising Provider Factory: " + Process.GetCurrentProcess().Threads.Count + " threads.");
                return factory;
            }
        }

        /**
         * This will shut down each provider class that make up this provider
         */
        public static void shutdown()
        {
            lock (locked)
            {
                if (factory != null)
                {
                    log.Debug("Finalising providers:");
                    foreach (ModelObjectInfo info in factory.providers.Keys)
                    {
                        try
                        {
                            log.Debug("--- " + info.GetObjectName());
                            factory.providers[info].Finalise();
                        }
                        catch (Exception ex)
                        {
                            log.Warn(ex.Message, ex);
                        }
                    }

                    log.Debug("Stopping provider threads:");
                    foreach (ModelObjectInfo info in factory.providerThreads.Keys)
                    {
                        try
                        {
                            log.Debug("--- " + info.GetObjectName());
                            factory.providerThreads[info].Abort(); 
                        }
                        catch (Exception ex)
                        {
                            log.Warn(ex.Message, ex);
                        }
                    }
                }
                log.Info("All providers are shut down.");
            }
        }

        /**
         * Returns an lazy loaded instance of this provider factory.
         * 
         * @return See Desc.
         */
        public static ProviderFactory getInstance()
        {
            if(factory == null)
            {
                return createFactory();
            }
            return factory;
        }

        public dynamic GetProvider(ModelObjectInfo objectInfo)
        {
            if ((objectInfo != null) && (StringUtils.NotEmpty(objectInfo.GetObjectName())))
            {
                // Not a functional service, look for object service instead
                ProviderClassInfo providerClassInfo = providerClasses[objectInfo];
                if (providerClassInfo != null)
                {
                    try
                    {
                        return providerClassInfo.GetClassInstance(null);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to instantiate a provider for " + objectInfo.GetObjectName() + ": " + ex.Message, ex);
                        return null;
                    }
                }
                else
                {
                    log.Error("No known provider for " + objectInfo.GetObjectName());
                    return null;
                }
            }
            else
            {
                log.Error("The ModelObjectInfo parameter is either null or does not have the ObjectName property set. This is required! No Provider returned.");
                return null;
            }
        }

        /*---------------------*/
        /*-- Private Methods --*/
        /*---------------------*/
        private ProviderFactory()
        {
            ProviderSettings settings = new ProviderSettings();
            InitialiseProviders(settings);
            StartProviders(settings);
        }

        private void InitialiseProviders(ProviderSettings settings)
        {
            foreach (Type type in settings.Classes)
            {
                log.Debug("Provider class to initialse: " + type.FullName);
                try
                {
                    ProviderClassInfo providerClassInfo = new ProviderClassInfo(type, new Type[] { });

                    IService provider = providerClassInfo.GetClassInstance(null);

                    ModelObjectInfo objectInfo = provider.GetMultiObjectClassInfo();
                    if ((objectInfo != null) && (StringUtils.NotEmpty(objectInfo.GetObjectName())))
                    {
                        log.Info("Adding provider for '" + objectInfo.GetObjectName() + "', using provider class '" + provider.GetType().FullName + "'.");

                        // First add it to the standard request/response dictionary
                        providerClasses[objectInfo] = providerClassInfo;

                        // Add it to dictionary of background threads
                        providers[objectInfo] = provider;

                        // Add it to dictionary of background threads
                        providerThreads[objectInfo] = new Thread(new ThreadStart(provider.Run));
                    }
                    else
                    {
                        log.Error("The ModelObjectInfo parameter is either null or does not have the ObjectName property set. This is required! Provider '" + provider.GetType().FullName + " not added to provider factory.");
                    }
                }
                catch (Exception ex)
                {

                    log.Error("Cannot create Provider Class " + type.FullName + ": " + ex.Message, ex);
                }
            }
        }

        private void StartProviders(ProviderSettings settings)
        {
            int delay = settings.StartupDelay;  //delay between threads in seconds
            log.Debug("Start up delay between providers is: " + delay + " seconds");

            int i = 0;
            foreach (Thread thread in providerThreads.Values)
            {
                Timer timer = new Timer((o) => {
                    thread.Start();
                }, null, (i * delay), Timeout.Infinite);
                i += 1000;
            }
        }
    }
}
