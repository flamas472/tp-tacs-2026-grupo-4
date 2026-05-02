using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Shared.Model;
using FiguritasApi.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FiguritasApi.Controllers;

[Authorize]
[ApiController]
[Route("api/exchange-proposals")]
public class ExchangeProposalsController : ControllerBase
{

    private readonly ExchangeProposalService _proposalService;
    private readonly ExchangeService _exchangeService;
    private readonly AuthService _authService;

    public ExchangeProposalsController(ExchangeProposalService proposalService, ExchangeService exchangeService , AuthService authService)
    {
        _proposalService = proposalService;
        _exchangeService = exchangeService;
        _authService = authService;
    }

    [HttpGet("/sent")]
    public ActionResult<List<ExchangeProposal>> GetSentProposals()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllSentProposals(userId);
        return Ok(proposals);
    }

    [HttpGet("/received")]
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
            // 1. Request validations
            if(exchangeProposalDTO.ProponentUserID < 0 || exchangeProposalDTO.ProposedUserID < 0)
                return BadRequest("Los usuarios deben ser válidos.");

            if(exchangeProposalDTO.OfferedStickersID.Any(id => id < 0) || exchangeProposalDTO.RequestedStickerID < 0)
                return BadRequest("Las figuritas ofrecidas o la figurita solicitada deben ser válidas.");
    
            var proposal = _proposalService.CreateExchangeProposal(
                exchangeProposalDTO.ProponentUserID,
                exchangeProposalDTO.ProposedUserID,
                exchangeProposalDTO.OfferedStickersID,
                exchangeProposalDTO.RequestedStickerID
            );

            if (proposal == null)
                return BadRequest("La propuesta creada es inválida.");
            else
                return CreatedAtAction(nameof(GetSentProposals), new { id = proposal.Id }, proposal);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }


    [HttpPost("{id}/accept")]
    public ActionResult AcceptProposal(int proposalID)
    {
        try
        {
            var proposal = _proposalService.GetProposalByID(proposalID);

            if(proposalID < 0 || proposal == null || !proposal.IsValid())
                return BadRequest("La propuesta debe ser válida.");

            if(proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Solo se pueden aceptar propuestas pendientes.");

            var userId = _authService.GetUserIdFromToken(User);
            if(proposal.ProposedID != userId)
                return BadRequest("Solo se pueden aceptar propuestas recibidas.");
       
            _proposalService.ChangeProposalStatus(proposalID, ExchangeProposalState.Accepted);

            // Al aceptar una propuesta de intercambio, se crea un intercambio.
            var exchange = _exchangeService.CreateExchange(proposal);
            
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/reject")]
    public ActionResult RejectProposal(int proposalID)
    {
        try
        {
            var proposal = _proposalService.GetProposalByID(proposalID);

            if(proposalID < 0 || proposal == null || !proposal.IsValid())
                return BadRequest("La propuesta debe ser válida.");

            if(proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Solo se pueden rechazar propuestas pendientes.");

            var userId = _authService.GetUserIdFromToken(User);
            if(proposal.ProposedID != userId)
                return BadRequest("Solo se pueden rechazar propuestas recibidas.");
            
            _proposalService.ChangeProposalStatus(proposalID, ExchangeProposalState.Rejected);
            
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/cancel")]
    public ActionResult CancelProposal(int proposalID)
    {
        try
        {
            var proposal = _proposalService.GetProposalByID(proposalID);

            if(proposalID < 0 || proposal == null || !proposal.IsValid())
                return BadRequest("La propuesta debe ser válida.");

            if(proposal.State != ExchangeProposalState.Pending)
                return BadRequest("Solo se pueden cancelar propuestas pendientes.");

            var userId = _authService.GetUserIdFromToken(User);
            if(proposal.ProponentID != userId)
                return BadRequest("Solo quien realizó la propuesta puede cancelarla.");

            _proposalService.ChangeProposalStatus(proposalID, ExchangeProposalState.Cancelled);
            
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

public class PostExchangeProposalDTO
{
    public required List<int> OfferedStickersID { get; set; } 

    public required int RequestedStickerID { get; set; } 

    public required int ProponentUserID {get; set; }

    public required int ProposedUserID {get; set; }
}
