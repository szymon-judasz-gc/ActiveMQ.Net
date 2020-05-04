using System;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Transactions;
using TaskExtensions = ActiveMQ.Net.InternalUtilities.TaskExtensions;

namespace ActiveMQ.Net.Transactions
{
    internal class Controller : SenderLink
    {
        private static readonly OutcomeCallback _onDeclareOutcome = OnDeclareOutcome;
        private static readonly OutcomeCallback _onDischargeOutcome = OnDischargeOutcome;

        public Controller(Session session) : base(session, GetName(), new Attach
        {
            Target = new Coordinator
            {
                Capabilities = new[] { TxnCapabilities.LocalTransactions }
            },
            Source = new Source(),
            SndSettleMode = SenderSettleMode.Unsettled,
            RcvSettleMode = ReceiverSettleMode.First
            
        }, null)
        {
        }

        public Task<byte[]> DeclareAsync(CancellationToken cancellationToken)
        {
            var message = new Amqp.Message(new Declare());
            var tcs = TaskExtensions.CreateTaskCompletionSource<byte[]>(cancellationToken);
            Send(message, null, _onDeclareOutcome, tcs);
            return tcs.Task;
        }

        public Task DischargeAsync(byte[] txnId, bool fail, CancellationToken cancellationToken)
        {
            var message = new Amqp.Message(new Discharge { TxnId = txnId, Fail = fail });
            var tcs = TaskExtensions.CreateTaskCompletionSource<bool>(cancellationToken);
            Send(message, null, _onDischargeOutcome, tcs);
            return tcs.Task;
        }

        private static string GetName()
        {
            return "controller-link-" + Guid.NewGuid().ToString("N").Substring(0, 5);
        }

        private static void OnDeclareOutcome(ILink link, Amqp.Message message, Outcome outcome, object state)
        {
            var tcs = (TaskCompletionSource<byte[]>) state;
            if (outcome.Descriptor.Code == MessageOutcomes.Declared.Descriptor.Code)
            {
                tcs.SetResult(((Declared) outcome).TxnId);
            }
            else if (outcome.Descriptor.Code == MessageOutcomes.Rejected.Descriptor.Code)
            {
                tcs.SetException(new AmqpException(((Rejected) outcome).Error));
            }
            else
            {
                tcs.SetCanceled();
            }
        }

        private static void OnDischargeOutcome(ILink link, Amqp.Message message, Outcome outcome, object state)
        {
            var tcs = (TaskCompletionSource<bool>) state;
            if (outcome.Descriptor.Code == MessageOutcomes.Accepted.Descriptor.Code)
            {
                tcs.SetResult(true);
            }
            else if (outcome.Descriptor.Code == MessageOutcomes.Rejected.Descriptor.Code)
            {
                tcs.SetException(new AmqpException(((Rejected) outcome).Error));
            }
            else
            {
                tcs.SetCanceled();
            }
        }
    }
}