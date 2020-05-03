using System.Transactions;

namespace ActiveMQ.Net.Transactions
{
    internal class Enlistment : IPromotableSinglePhaseNotification
    {
        public byte[] Promote()
        {
            throw new System.NotImplementedException();
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public void Rollback(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            throw new System.NotImplementedException();
        }

        public void SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            throw new System.NotImplementedException();
        }
    }
}