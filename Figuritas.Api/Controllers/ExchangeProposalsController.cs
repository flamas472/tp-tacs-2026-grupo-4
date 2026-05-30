using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.Model;
using Figuritas.Shared.DTO.request;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace Figuritas.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ExchangeProposalsController : ControllerBase
{
    private readonly ExchangeProposalService _proposalService;
    private readonly ExchangeService _exchangeService;
    private readonly AuthService _authService;

    public ExchangeProposalsController(ExchangeProposalService proposalService, ExchangeService exchangeService, AuthService authService)
    {
        _proposalService = proposalService;
        _exchangeService = exchangeService;
        _authService = authService;
    }

    [HttpGet("sent")]
    public ActionResult<List<ExchangeProposal>> GetSentProposals()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllSentProposals(userId);
        return Ok(proposals);
    }

    [HttpGet("received")]
    public ActionResult<List<ExchangeProposal>> GetReceivedProposals()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllReceivedProposals(userId);
        return Ok(proposals);
    }

    [HttpPost]
    public ActionResult<ExchangeProposal> PostExchangeProposal(PostExchangeProposalDTO exchangeProposalDTO)
    {
        try
        {
            var proponentId = _authService.GetUserIdFromToken(User);

            if (exchangeProposalDTO.ProposedUserID <= 0)
                return BadRequest("Users must be valid.");

            if (exchangeProposalDTO.OfferedStickersID.Any(id => id <= 0) || exchangeProposalDTO.RequestedStickerID <= 0)
                return BadRequest("Offered stickers and requested sticker must be valid.");

            var proposal = _proposalService.CreateExchangeProposal(
                proponentId,
                exchangeProposalDTO.ProposedUserID,
                exchangeProposalDTO.OfferedStickersID,
                exchangeProposalDTO.RequestedStickerID
            );

            return CreatedAtAction(nameof(GetSentProposals), null, proposal);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/accept")]
    public ActionResult AcceptProposal(int id)
    {
        try
        {
            if (id <= 0)
                return BadRequest("Invalid proposal ID.");

            var userId = _authService.GetUserIdFromToken(User);

            var proposal = _proposalService.GetProposalByID(id);
            if (proposal == null)
                return NotFound("Proposal not found.");

            if (proposal.ProposedID != userId)
                return BadRequest("Only the recipient can accept a proposal.");

            var accepted = _proposalService.AcceptProposalAtomically(id);

            var exchange = _exchangeService.CreateExchange(accepted);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/reject")]
    public ActionResult RejectProposal(int id)
    {
        try
        {
            var proposal = _proposalService.GetProposalByID(id);

            if (id <= 0 || proposal == null || !proposal.IsValid())
                return BadRequest("Proposal must be valid.");

            if (proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Only pending proposals can be rejected.");

            var userId = _authService.GetUserIdFromToken(User);
            if (proposal.ProposedID != userId)
                return BadRequest("Only the recipient can reject a proposal.");

            _proposalService.ChangeProposalStatus(id, ExchangeProposalState.Rejected);

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/cancel")]
    public ActionResult CancelProposal(int id)
    {
        try
        {
            var proposal = _proposalService.GetProposalByID(id);

            if (id <= 0 || proposal == null || !proposal.IsValid())
                return BadRequest("Proposal must be valid.");

            if (proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Only pending proposals can be cancelled.");

            var userId = _authService.GetUserIdFromToken(User);
            if (proposal.ProponentID != userId)
                return BadRequest("Only the proponent can cancel a proposal.");

            _proposalService.ChangeProposalStatus(id, ExchangeProposalState.Cancelled);

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
