﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarn.IoC
{
    public interface IContainer : IDisposable
    {
        void Register<TAbstract, TConcrete>(string instanceName = null) 
            where TAbstract : class
            where TConcrete : class, TAbstract, new();

        void Register<TAbstract, TConcrete>(TConcrete instance, string instanceName = null)
            where TAbstract : class
            where TConcrete : class, TAbstract;

        void Register<TAbstract>(Func<TAbstract> createInstanceFactory, string instanceName = null)
            where TAbstract : class;

        bool IsRegistered<TAbstract>(string instanceName = null)
            where TAbstract : class;

        TAbstract Resolve<TAbstract>(string instanceName = null)
           where TAbstract : class;
    }
}
