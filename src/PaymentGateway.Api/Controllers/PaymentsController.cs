using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Queries;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class PaymentsController : Controller
{
    private readonly ProcessPaymentCommandHandler _processPayment;
    private readonly GetPaymentQueryHandler _getPayment;

    public PaymentsController(ProcessPaymentCommandHandler processPayment, GetPaymentQueryHandler getPayment)
    {
        _processPayment = processPayment;
        _getPayment = getPayment;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RejectedPaymentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PostPayment(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _processPayment.HandleAsync(ProcessPaymentCommand.From(request), cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(
                title: "Acquiring bank unavailable",
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway);
        }

        var response = PostPaymentResponse.From(result.Value!);
        return CreatedAtAction(nameof(GetPayment), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _getPayment.Handle(new GetPaymentQuery(id));

        return payment is null ? NotFound() : payment;
    }
}
