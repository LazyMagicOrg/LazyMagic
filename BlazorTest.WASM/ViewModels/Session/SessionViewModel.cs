﻿using static System.Formats.Asn1.AsnWriter;

namespace BlazorTest.ViewModels;

[Factory]
public class SessionViewModel : LzSessionViewModelAuth, ISessionViewModel, ILzTransient
{
    public SessionViewModel(
        [FactoryInject] ILoggerFactory loggerFactory, // singleton  
        [FactoryInject] IInternetConnectivitySvc internetConnectivity, // singleton
        [FactoryInject] ILzClientConfig clientConfig, // singleton
        [FactoryInject] ILzMessages messages, // singleton
        [FactoryInject] IAuthProcess authProcess, // transient
        [FactoryInject] ILzHost lzHost // singleton
        )
        : base(loggerFactory, authProcess, clientConfig, internetConnectivity, messages)
    {
        authProcess.SetAuthenticator(clientConfig.AuthConfigs["api"]);

    }
}
