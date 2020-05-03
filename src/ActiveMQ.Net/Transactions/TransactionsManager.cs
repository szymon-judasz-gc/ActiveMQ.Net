using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Transactions;
using Nito.AsyncEx;

namespace ActiveMQ.Net.Transactions
{
    internal class TransactionsManager
    {
        private readonly Amqp.Connection _connection;
        private readonly AsyncLock _mutex = new AsyncLock();
        private Controller _controller;

        public TransactionsManager(Amqp.Connection connection)
        {
            _connection = connection;
        }

        public async ValueTask<TransactionalState> GetTransactionalStateAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            if (transaction == null)
            {
                return null;
            }

            if (transaction.TransactionalState != null)
            {
                return transaction.TransactionalState;
            }

            using (await _mutex.LockAsync().ConfigureAwait(false))
            {
                await EnlistAsync(transaction, cancellationToken).ConfigureAwait(false);
            }

            return transaction.TransactionalState;
        }

        private async Task EnlistAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            var session = new Session(_connection);
            _controller = new Controller(session);
            var txnId = await _controller.DeclareAsync(cancellationToken);
            var transactionalState = new TransactionalState { TxnId = txnId };
            transaction.Initialize(transactionalState, _controller);
        }
    }
}