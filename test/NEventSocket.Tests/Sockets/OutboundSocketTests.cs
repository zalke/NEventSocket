﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OutboundSocketTests.cs" company="Business Systems (UK) Ltd">
//   Copyright © Business Systems (UK) Ltd and contributors. All rights reserved.
// </copyright>
// <summary>
//   Defines the OutboundSocketTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace NEventSocket.Tests.Sockets
{
    using System;
    using System.Reactive.Linq;
    using System.Threading.Tasks;

    using NEventSocket.FreeSwitch;
    using NEventSocket.Logging;
    using NEventSocket.Logging.LogProviders;
    using NEventSocket.Tests.Fakes;
    using NEventSocket.Tests.TestSupport;

    using Xunit;

    public class OutboundSocketTests
    {
        public OutboundSocketTests()
        {
            LogProvider.SetCurrentLogProvider(new ColouredConsoleLogProvider());
        }

        [Fact(Timeout = 5000)]
        public void Disposing_the_listener_completes_the_message_observables()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool messagesObservableCompleted = false;
                bool eventsObservableCompleted = false;

                listener.Connections.Subscribe((connection) =>
                {
                    connected = true;
                    connection.Messages.Subscribe(_ => { }, () => messagesObservableCompleted = true);
                    connection.Events.Subscribe(_ => { }, () => eventsObservableCompleted = true);
                });

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    ThreadUtils.WaitUntil(() => connected);
                    listener.Dispose(); // will dispose the socket

                    ThreadUtils.WaitUntil(() => messagesObservableCompleted);
                    ThreadUtils.WaitUntil(() => eventsObservableCompleted);

                    Assert.True(connected, "Expect a connection to have been made.");
                    Assert.True(messagesObservableCompleted, "Expect the BasicMessage observable to be completed");
                    Assert.True(eventsObservableCompleted, "Expect the EventMessage observable to be completed");
                }
            }
        }

        [Fact(Timeout = 5000)]
        public void When_FreeSwitch_disconnects_it_completes_the_message_observables()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool messagesObservableCompleted = false;
                bool eventsObservableCompleted = false;

                listener.Connections.Subscribe((connection) =>
                {
                    connected = true;
                    connection.Messages.Subscribe(_ => { }, () => messagesObservableCompleted = true);
                    connection.Events.Subscribe(_ => { }, () => eventsObservableCompleted = true);
                });

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    ThreadUtils.WaitUntil(() => connected);
                    client.Dispose();

                    ThreadUtils.WaitUntil(() => messagesObservableCompleted);
                    ThreadUtils.WaitUntil(() => eventsObservableCompleted);

                    Assert.True(connected, "Expect a connection to have been made.");
                    Assert.True(messagesObservableCompleted, "Expect the BasicMessage observable to be completed");
                    Assert.True(eventsObservableCompleted, "Expect the EventMessage observable to be completed");
                }
            }
        }

        [Fact(Timeout = 5000)]
        public async Task Calling_Connect_on_a_new_OutboundSocket_should_populate_the_ChannelData()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                EventMessage channelData = null;

                listener.Connections.Subscribe(
                    async (socket) =>
                    {
                        channelData = await socket.Connect();
                    });

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    client.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ => await client.SendChannelDataEvent());

                    ThreadUtils.WaitUntil(() => channelData != null);

                    Assert.NotNull(channelData);
                    Assert.Equal(ChannelState.Execute, channelData.ChannelState);
                    Assert.Equal("RINGING", channelData.Headers["Channel-Call-State"]);
                }
            }
        }
    }
}