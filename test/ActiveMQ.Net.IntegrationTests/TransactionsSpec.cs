using System;
using System.Threading;
using System.Threading.Tasks;
using ActiveMQ.Net.Transactions;
using Xunit;
using Xunit.Abstractions;

namespace ActiveMQ.Net.IntegrationTests
{
    public class TransactionsSpec : ActiveMQNetIntegrationSpec
    {
        public TransactionsSpec(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Should_deliver_messages_when_transaction_committed()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_deliver_messages_when_transaction_committed);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction = new Transaction();
            await producer.SendAsync(new Message("foo1"), transaction);
            await producer.SendAsync(new Message("foo2"), transaction);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumer.ReceiveAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token));

            await transaction.CommitAsync();

            var msg1 = await consumer.ReceiveAsync(CancellationToken);
            var msg2 = await consumer.ReceiveAsync(CancellationToken);
            Assert.Equal("foo1", msg1.GetBody<string>());
            Assert.Equal("foo2", msg2.GetBody<string>());
            
            consumer.Accept(msg1);
            consumer.Accept(msg2);
        }

        [Fact]
        public async Task Should_not_deliver_any_messages_when_transaction_rolled_back()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_deliver_messages_when_transaction_committed);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction = new Transaction();
            await producer.SendAsync(new Message("foo1"), transaction);
            await producer.SendAsync(new Message("foo2"), transaction);

            await transaction.RollbackAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumer.ReceiveAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token));
        }
    }
}