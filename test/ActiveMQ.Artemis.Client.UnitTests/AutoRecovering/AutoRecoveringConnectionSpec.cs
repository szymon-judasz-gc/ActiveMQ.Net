﻿using System;
using System.Threading;
using System.Threading.Tasks;
using ActiveMQ.Artemis.Client.AutoRecovering.RecoveryPolicy;
using ActiveMQ.Artemis.Client.InternalUtilities;
using ActiveMQ.Artemis.Client.UnitTests.Utils;
using Amqp.Framing;
using Amqp.Handler;
using Xunit;
using Xunit.Abstractions;

namespace ActiveMQ.Artemis.Client.UnitTests.AutoRecovering
{
    public class AutoRecoveringConnectionSpec : ActiveMQNetSpec
    {
        public AutoRecoveringConnectionSpec(ITestOutputHelper output) : base(output)
        {
        }
        
        [Fact]
        public async Task Should_reconnect_when_broker_is_available_after_outage_is_over()
        {
            var endpoint = GetUniqueEndpoint();
            var connectionOpened = new ManualResetEvent(false);

            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.ConnectionRemoteOpen:
                        connectionOpened.Set();
                        break;
                }
            });

            var host1 = CreateOpenedContainerHost(endpoint, testHandler);

            var connection = await CreateConnection(endpoint);
            Assert.NotNull(connection);
            Assert.True(connectionOpened.WaitOne(Timeout));

            host1.Dispose();

            connectionOpened.Reset();
            var host2 = CreateOpenedContainerHost(endpoint, testHandler);

            Assert.True(connectionOpened.WaitOne(Timeout));

            await DisposeUtil.DisposeAll(connection, host2);
        }

        [Fact]
        public async Task Should_not_try_to_reconnect_when_connection_explicitly_closed()
        {
            var endpoint = GetUniqueEndpoint();
            var connectionOpened = new ManualResetEvent(false);

            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.ConnectionRemoteOpen:
                        connectionOpened.Set();
                        break;
                }
            });

            using var host = CreateOpenedContainerHost(endpoint, testHandler);

            await using var connection = await CreateConnection(endpoint);
            Assert.NotNull(connection);
            Assert.True(connectionOpened.WaitOne(Timeout));

            connectionOpened.Reset();
            await connection.DisposeAsync();

            Assert.False(connectionOpened.WaitOne(ShortTimeout));
        }
        
        [Fact]
        public async Task Should_recreate_producers_on_connection_recovery()
        {
            var endpoint = GetUniqueEndpoint();
            var producersAttached = new CountdownEvent(2);
            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.LinkRemoteOpen when @event.Context is Attach attach && attach.Role:
                        producersAttached.Signal();
                        break;
                }
            });

            var host1 = CreateOpenedContainerHost(endpoint, testHandler);

            var connection = await CreateConnection(endpoint);
            await connection.CreateProducerAsync("a1", RoutingType.Anycast);
            await connection.CreateProducerAsync("a2", RoutingType.Anycast);

            Assert.True(producersAttached.Wait(Timeout));
            producersAttached.Reset();

            host1.Dispose();

            var host2 = CreateOpenedContainerHost(endpoint, testHandler);

            Assert.True(producersAttached.Wait(Timeout));

            await DisposeUtil.DisposeAll(connection, host2);
        }

        [Fact]
        public async Task Should_not_recreate_disposed_producers()
        {
            var endpoint = GetUniqueEndpoint();
            var producerAttached = new ManualResetEvent(false);
            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.LinkRemoteOpen when @event.Context is Attach attach && attach.Role:
                        producerAttached.Set();
                        break;
                }
            });

            var host1 = CreateOpenedContainerHost(endpoint, testHandler);

            var connection = await CreateConnection(endpoint);
            var producer = await connection.CreateProducerAsync("a1");

            Assert.True(producerAttached.WaitOne(Timeout));
            await producer.DisposeAsync();
            
            producerAttached.Reset();
            host1.Dispose();

            using var host2 = CreateOpenedContainerHost(endpoint, testHandler);

            Assert.False(producerAttached.WaitOne(ShortTimeout));
        }

        [Fact]
        public async Task Should_recreate_consumers_on_connection_recovery()
        {
            var endpoint = GetUniqueEndpoint();
            var consumersAttached = new CountdownEvent(2);
            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.LinkRemoteOpen when @event.Context is Attach attach && !attach.Role:
                        consumersAttached.Signal();
                        break;
                }
            });

            var host1 = CreateOpenedContainerHost(endpoint, testHandler);

            var connection = await CreateConnection(endpoint);
            await connection.CreateConsumerAsync("a1", RoutingType.Anycast);
            await connection.CreateConsumerAsync("a1", RoutingType.Anycast);

            Assert.True(consumersAttached.Wait(Timeout));
            consumersAttached.Reset();

            host1.Dispose();

            var host2 = CreateOpenedContainerHost(endpoint, testHandler);

            Assert.True(consumersAttached.Wait(Timeout));

            await DisposeUtil.DisposeAll(connection, host2);
        }
        
        [Fact]
        public async Task Should_not_recreate_disposed_consumers()
        {
            var endpoint = GetUniqueEndpoint();
            var consumerAttached = new ManualResetEvent(false);
            var testHandler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.LinkRemoteOpen when @event.Context is Attach attach && !attach.Role:
                        consumerAttached.Set();
                        break;
                }
            });

            var host1 = CreateOpenedContainerHost(endpoint, testHandler);

            var connection = await CreateConnection(endpoint);
            var consumer = await connection.CreateConsumerAsync("a1", RoutingType.Anycast);

            Assert.True(consumerAttached.WaitOne(Timeout));
            await consumer.DisposeAsync();
            
            consumerAttached.Reset();
            host1.Dispose();

            var host2 = CreateOpenedContainerHost(endpoint, testHandler);

            Assert.False(consumerAttached.WaitOne(ShortTimeout));

            await DisposeUtil.DisposeAll(connection, host2);
        }

        [Fact]
        public async Task Should_connect_to_first_endpoint_when_auto_recovering_disabled()
        {
            var (host1, connectedToHost1) = CreateHost();
            var (host2, connectedToHost2) = CreateHost();

            var connectionFactory = CreateConnectionFactory();
            connectionFactory.AutomaticRecoveryEnabled = false;

            var connection = await connectionFactory.CreateAsync(new[] { host1.Endpoint, host2.Endpoint });

            Assert.True(connection.IsOpened);
            Assert.True(connectedToHost1.WaitOne(Timeout));
            Assert.False(connectedToHost2.WaitOne(ShortTimeout));

            await DisposeUtil.DisposeAll(connection, host1, host2);
        }
        
        [Fact]
        public async Task Should_connect_to_the_second_endpoint_when_first_endpoint_disconnected()
        {
            var host1 = CreateContainerHost();
            var (host2, connectedToHost2) = CreateHost();

            var connection = await CreateConnection(new[] { host1.Endpoint, host2.Endpoint });

            Assert.True(connectedToHost2.WaitOne(Timeout));
            Assert.True(connection.IsOpened);
        }

        [Fact]
        public async Task Should_reconnect_to_the_second_endpoint_after_first_endpoint_disconnected()
        {
            var (host1, connectedToHost1) = CreateHost();
            var (host2, connectedToHost2) = CreateHost();

            var connection = await CreateConnection(new[] { host1.Endpoint, host2.Endpoint });

            Assert.True(connection.IsOpened);
            Assert.True(connectedToHost1.WaitOne(Timeout));
            Assert.False(connectedToHost2.WaitOne(ShortTimeout));

            host1.Dispose();
            
            Assert.True(connectedToHost2.WaitOne(Timeout));
            Assert.False(connectedToHost1.WaitOne(ShortTimeout));
            Assert.True(connection.IsOpened);

            await DisposeUtil.DisposeAll(connection, host2);
        }

        [Fact]
        public async Task Should_trigger_ConnectionRecovered_when_connection_recovered()
        {
            var host1 = CreateOpenedContainerHost();
            var host2 = CreateOpenedContainerHost();

            var connection = await CreateConnection(new[] { host1.Endpoint, host2.Endpoint });

            var connectionRecovered = new AutoResetEvent(false);
            Endpoint fallbackEndpoint = null;
            connection.ConnectionRecovered += (sender, args) =>
            {
                fallbackEndpoint = args.Endpoint;
                connectionRecovered.Set();
            };

            Assert.True(connection.IsOpened);

            host1.Dispose();
            
            Assert.True(connectionRecovered.WaitOne(Timeout));
            Assert.Equal(host2.Endpoint, fallbackEndpoint);

            await DisposeUtil.DisposeAll(connection, host2);
        }

        [Fact]
        public async Task Should_trigger_ConnectionRecoveryError_when_connection_recovery_failed()
        {
            var host = CreateOpenedContainerHost();

            var connectionFactory = CreateConnectionFactory();
            connectionFactory.RecoveryPolicy = RecoveryPolicyFactory.ConstantBackoff(TimeSpan.FromMilliseconds(10), retryCount: 1);

            await using var connection = await connectionFactory.CreateAsync(host.Endpoint);

            var connectionRecoveryFailed = new AutoResetEvent(false);
            Exception connectionRecoveryError = null;
            connection.ConnectionRecoveryError += (sender, args) =>
            {
                connectionRecoveryError = args.Exception;
                connectionRecoveryFailed.Set();
            };

            Assert.True(connection.IsOpened);

            host.Dispose();
            
            Assert.True(connectionRecoveryFailed.WaitOne(Timeout));
            Assert.NotNull(connectionRecoveryError);
        }

        private static (TestContainerHost host, AutoResetEvent connected) CreateHost()
        {
            var endpoint = GetUniqueEndpoint();
            var connected = new AutoResetEvent(false);
            var handler = new TestHandler(@event =>
            {
                switch (@event.Id)
                {
                    case EventId.ConnectionRemoteOpen:
                        connected.Set();
                        break;
                }
            });
            var host = CreateOpenedContainerHost(endpoint, handler);
            return (host, connected);
        }
    }
}