﻿using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public class BusyWallStartRequestMessageData
    {
    }

    public sealed class BusyWallStartRequestMessage : ValueChangedMessage<BusyWallStartRequestMessageData>
    {
        public BusyWallStartRequestMessage() : base(new BusyWallStartRequestMessageData())
        {
        }
    }

    public class BusyWallExitRequestMessageData
    {
    }

    public sealed class BusyWallExitRequestMessage : ValueChangedMessage<BusyWallExitRequestMessageData>
    {
        public BusyWallExitRequestMessage() : base(new BusyWallExitRequestMessageData())
        {
        }
    }

    public class BusyWallCanceledMessageData
    {
    }

    public sealed class BusyWallCanceledMessage : ValueChangedMessage<BusyWallCanceledMessageData>
    {
        public BusyWallCanceledMessage() : base(new BusyWallCanceledMessageData())
        {
        }
    }


    public static class BusyWallCanceledMessageExtensions
    {
        public static async Task<T> WorkWithBusyWallAsync<T>(this IMessenger messenger, Func<CancellationToken, Task<T>> action, CancellationToken actionCt)
        {
            object dummy = new object();
            using (var manualCts = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(manualCts.Token, actionCt))
            {
                var ct = linkedCts.Token;
                messenger.Register<BusyWallCanceledMessage>(dummy, (r, m) => { manualCts.Cancel(); });
                try
                {
                    messenger.Send<BusyWallStartRequestMessage>();
                    return await action(ct);
                }
                finally
                {
                    messenger.Send<BusyWallExitRequestMessage>();
                    messenger.Unregister<BusyWallCanceledMessage>(dummy);
                }
            }
        }

        public static async Task WorkWithBusyWallAsync(this IMessenger messenger, Func<CancellationToken, Task> action, CancellationToken actionCt)
        {
            object dummy = new object();
            using (var manualCts = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(manualCts.Token, actionCt))
            {
                var ct = linkedCts.Token;
                messenger.Register<BusyWallCanceledMessage>(dummy, (r, m) => { manualCts.Cancel(); });
                try
                {
                    messenger.Send<BusyWallStartRequestMessage>();
                    await action(ct);
                }
                finally
                {
                    messenger.Send<BusyWallExitRequestMessage>();
                    messenger.Unregister<BusyWallCanceledMessage>(dummy);
                }
            }
        }
    }
}
