using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("api/exchange-proposals")]
public class ExchangeProposalsController : ControllerBase
{
    private readonly ExchangeProposalRepository _proposalRepo;

    public ExchangeProposalsController(ExchangeProposalRepository proposalRepo)
    {
        _proposalRepo = proposalRepo;
    }

    [HttpGet]
    public ActionResult<List<ExchangeProposal>> GetProposals()
    {
        var proposals = _proposalRepo.GetAll();
        return Ok(proposals);
    }

    [HttpPost]
    public ActionResult<ExchangeProposal> PostProposal(ExchangeProposal proposal)
    {
        _proposalRepo.Add(proposal);
        return CreatedAtAction(nameof(GetProposals), new { id = proposal.Id }, proposal);
    }

    [HttpPost("{id}/accept")]
    public ActionResult AcceptProposal(int id)
    {
        var proposal = _proposalRepo.GetById(id);
        if (proposal == null) return NotFound("Proposal not found");
        proposal.State = ExchangeProposalState.Accepted;
        return Ok();
    }

    [HttpPost("{id}/reject")]
    public ActionResult RejectProposal(int id)
    {
        var proposal = _proposalRepo.GetById(id);
        if (proposal == null) return NotFound("Proposal not found");
        proposal.State = ExchangeProposalState.Rejected;
        return Ok();
    }
}