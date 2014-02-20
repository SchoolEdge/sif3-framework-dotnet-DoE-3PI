﻿/*
 * Copyright 2014 Systemic Pty Ltd
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using NHibernate;
using NHibernate.Cfg;

namespace Sif.Framework.Persistence.NHibernate
{

    class EnvironmentProviderSessionFactory : IBaseSessionFactory
    {
        private static EnvironmentProviderSessionFactory environmentProviderSessionFactory;

        private ISessionFactory SessionFactory { get; set; }

        private EnvironmentProviderSessionFactory()
        {

        }

        public static EnvironmentProviderSessionFactory Instance
        {

            get
            {

                if (environmentProviderSessionFactory == null)
                {
                    environmentProviderSessionFactory = new EnvironmentProviderSessionFactory();
                    environmentProviderSessionFactory.SessionFactory = new Configuration().Configure().BuildSessionFactory();
                }

                return environmentProviderSessionFactory;
            }

        }

        public ISession OpenSession()
        {
            return SessionFactory.OpenSession();
        }

    }

}
