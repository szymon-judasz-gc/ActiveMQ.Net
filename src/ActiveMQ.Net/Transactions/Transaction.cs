using System;
using System.Threading;
using System.Threading.Tasks;
using Amqp.Transactions;

namespace ActiveMQ.Net.Transactions
{
    public class Transaction : IAsyncDisposable
    {
        private TransactionalState _transactionalState;
        private Controller _controller;

        public TransactionalState TransactionalState => _transactionalState;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _controller.DischargeAsync(_transactionalState.TxnId, false, cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return _controller.DischargeAsync(_transactionalState.TxnId, true, cancellationToken);
        }

        internal void Initialize(TransactionalState transactionalState, Controller controller)
        {
            _transactionalState = transactionalState;
            _controller = controller;
        }

        public async ValueTask DisposeAsync()
        {
            
        }
    }
}