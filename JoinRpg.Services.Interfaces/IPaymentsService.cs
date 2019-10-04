using System;
using System.Threading.Tasks;
using PscbApi.Models;

namespace JoinRpg.Services.Interfaces
{

    /// <summary>
    /// Base class for payment requests
    /// </summary>
    public class PaymentRequest
    {
        /// <summary>
        /// Database Id of a payer
        /// </summary>
        public int PayerId { get; set; }

        /// <summary>
        /// How much to pay
        /// </summary>
        public int Money { get; set; }

        /// <summary>
        /// Comment added by payer
        /// </summary>
        public string CommentText { get; set; }

        /// <summary>
        /// Date and time of the operation
        /// </summary>
        public DateTime OperationDate { get; set; }
    }

    /// <summary>
    /// Payment request for game fee
    /// </summary>
    public class ClaimPaymentRequest : PaymentRequest
    {
        /// <summary>
        /// Database Id of a project
        /// </summary>
        public int ProjectId { get; set; }

        /// <summary>
        /// Database Id of a claim
        /// </summary>
        public int ClaimId { get; set; }
    }

    /// <summary>
    /// Base class for payment results
    /// </summary>
    public class PaymentContext
    {
        /// <summary>
        /// true if payment was accepted
        /// </summary>
        public bool Accepted { get; set; }

        /// <summary>
        /// Payment request description built by bank API
        /// </summary>
        public PaymentRequestDescriptor RequestDescriptor { get; set; }
    }

    /// <summary>
    /// Result of claim payment
    /// </summary>
    public class ClaimPaymentContext : PaymentContext
    {
    }

    /// <summary>
    /// Base class for payment results
    /// </summary>
    public class PaymentResultContext
    {
        /// <summary>
        /// true if payment was successfully made
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Database Id of a finance operation
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// Bank response info
        /// </summary>
        public BankResponseInfo BankResponse { get; set; }
    }

    /// <summary>
    /// Claim payment result 
    /// </summary>
    public class ClaimPaymentResultContext : PaymentResultContext
    {
        /// <summary>
        /// Database Id of a project
        /// </summary>
        public int ProjectId { get; set; }

        /// <summary>
        /// Database Id of a claim
        /// </summary>
        public int ClaimId { get; set; }
    }

    /// <summary>
    /// Payments service
    /// </summary>
    public interface IPaymentsService
    {
        /// <summary>
        /// Creates finance operation and returns payment context
        /// </summary>
        /// <param name="request">Payment request</param>
        Task<ClaimPaymentContext> InitiateClaimPaymentAsync(ClaimPaymentRequest request);

        /// <summary>
        /// Sets payment result and returns payment context
        /// </summary>
        /// <param name="result">Payment result context</param>
        Task SetClaimPaymentResultAsync(ClaimPaymentResultContext result);
    }
}
