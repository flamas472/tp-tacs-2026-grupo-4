using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Api.Services;
using Figuritas.Shared.Model.Intercambios;
using Microsoft.AspNetCore.Authorization;

namespace Figuritas.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/exchange-proposals")]
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

    [HttpGet("{id}")]
    public ActionResult<ExchangeProposalResponseDTO> GetExchangeProposalById(int id)
    {
        var dto = _proposalService.GetProposalDtoByID(id);
        if (dto == null)
            return NotFound("Proposal not found.");
        return Ok(dto);
    }

    [HttpGet("sent")]
    public ActionResult<List<ExchangeProposalResponseDTO>> GetSentProposals()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllSentProposals(userId);
        return Ok(proposals);
    }

    [HttpGet("received")]
    public ActionResult<List<ExchangeProposalResponseDTO>> GetReceivedProposals()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllReceivedProposals(userId);
        return Ok(proposals);
    }

    [HttpPost]
    public ActionResult<ExchangeProposalResponseDTO> PostExchangeProposal(PostExchangeProposalRequestDTO exchangeProposalDTO)
    {
        try
        {
            var proponentId = _authService.GetUserIdFromToken(User);

            if (exchangeProposalDTO.ProposedUserId <= 0)
                return BadRequest("Users must be valid.");

            if (exchangeProposalDTO.OfferedUserStickerIds == null || exchangeProposalDTO.OfferedUserStickerIds.Any(id => id <= 0) || exchangeProposalDTO.RequestedUserStickerId <= 0)
                return BadRequest("Offered stickers and requested sticker must be valid.");

            var responseDto = _proposalService.CreateExchangeProposal(proponentId, exchangeProposalDTO);

            return CreatedAtAction(nameof(GetExchangeProposalById), new { id = responseDto.Id }, responseDto);
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

            if (proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Only pending proposals can be accepted.");

            var accepted = _proposalService.AcceptProposalAtomically(id);

            _exchangeService.CreateExchange(accepted);

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
            if (id <= 0)
                return BadRequest("Invalid proposal ID.");

            var proposal = _proposalService.GetProposalByID(id);
            if (proposal == null)
                return NotFound("Proposal not found.");

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
            if (id <= 0)
                return BadRequest("Invalid proposal ID.");

            var proposal = _proposalService.GetProposalByID(id);
            if (proposal == null)
                return NotFound("Proposal not found.");

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
