﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JoinRpg.Data.Interfaces;
using JoinRpg.DataModel;

namespace JoinRpg.Dal.Impl.Repositories
{
  [UsedImplicitly]
  public class ProjectRepository : RepositoryImplBase, IProjectRepository
  {
    public ProjectRepository(MyDbContext ctx) : base(ctx) 
    {
    }

    public async Task<IEnumerable<Project>> GetActiveProjectsWithClaimCount()
      => await ActiveProjects.Include(p=> p.Claims).ToListAsync();

    private IQueryable<Project> ActiveProjects => AllProjects.Where(project => project.Active);
    private IQueryable<Project> AllProjects => Ctx.ProjectsSet.Include(p => p.ProjectAcls); 

    public IEnumerable<Project> GetMyActiveProjects(int? userInfoId)
      => userInfoId == null ? Enumerable.Empty<Project>() :  ActiveProjects.Where(MyProjectPredicate(userInfoId));

    public Task<Project> GetProjectAsync(int project) => AllProjects.SingleOrDefaultAsync(p => p.ProjectId == project);

    public Task<Project> GetProjectWithDetailsAsync(int project)
      => AllProjects
        .Include(p => p.Details)
        .Include(p => p.ProjectAcls.Select(a => a.User))
        .SingleOrDefaultAsync(p => p.ProjectId == project);

    public Task<CharacterGroup> LoadGroupAsync(int projectId, int characterGroupId)
    {
      return
        Ctx.Set<CharacterGroup>()
          .Include(cg => cg.Project)
          .SingleOrDefaultAsync(cg => cg.CharacterGroupId ==characterGroupId && cg.ProjectId == projectId);
    }

    public Task<CharacterGroup> LoadGroupWithChildsAsync(int projectId, int characterGroupId)
    {
      return
        Ctx.Set<CharacterGroup>()
          .Include(cg => cg.Project)
          .Include(cg => cg.Characters)
          .Include(cg => cg.ChildGroups)
          .SingleOrDefaultAsync(cg => cg.CharacterGroupId == characterGroupId && cg.ProjectId == projectId);
    }

    private static Expression<Func<Project, bool>> MyProjectPredicate(int? userInfoId)
    {
      if (userInfoId == null)
      {
        return project => false;
      }
      return project => project.ProjectAcls.Any(projectAcl => projectAcl.UserId == userInfoId);
    }


    public Task<Character> GetCharacterAsync(int projectId, int characterId)
    {
      return
        Ctx.Set<Character>()
          .Include(c => c.Project)
          .Include(c => c.Project.ProjectFields)
          .Include(c => c.Claims.Select(claim => claim.Player))
          .SingleOrDefaultAsync(e => e.CharacterId == characterId && e.ProjectId == projectId);
    }

    public Task<Claim> GetClaim(int projectId, int claimId)
    {
      return
        Ctx.ClaimSet
          .Include(c => c.Project)
          .Include(c => c.Project.ProjectAcls)
          .Include(c => c.Character)
          .Include(c => c.Player)
          .Include(c => c.Player.Claims)
          .SingleOrDefaultAsync(e => e.ClaimId == claimId && e.ProjectId == projectId);
    }

    public Task<Claim> GetClaimWithDetails(int projectId, int claimId)
    {
      return
        Ctx.ClaimSet
          .Include(c => c.Project)
          .Include(c => c.Project.ProjectAcls)
          .Include(c => c.Project.Claims)
          .Include(c => c.Project.CharacterGroups)
          .Include(c => c.Project.Characters)
          .Include(c => c.Character)
          .Include(c => c.Player)
          .Include(c => c.Player.Claims)
          .Include(c => c.Comments.Select(com => com.Finance)) 
          .SingleOrDefaultAsync(e => e.ClaimId == claimId && e.ProjectId == projectId);
    }

    public async Task<IList<Character>> LoadCharacters(int projectId, ICollection<int> characterIds)
    {
      return await Ctx.Set<Character>().Where(cg => cg.ProjectId == projectId && characterIds.Contains(cg.CharacterId)).ToListAsync();
    }

    public async Task<IList<CharacterGroup>> LoadGroups(int projectId, ICollection<int> groupIds)
    {
      return await Ctx.Set<CharacterGroup>().Where(cg => cg.ProjectId == projectId && groupIds.Contains(cg.CharacterGroupId)).ToListAsync();
    }

    public Task<ProjectCharacterField> GetProjectField(int projectId, int projectCharacterFieldId)
    {
      return Ctx.Set<ProjectCharacterField>()
        .Include(f => f.Project)
        .Include(f => f.DropdownValues)
        .SingleOrDefaultAsync(f => f.ProjectCharacterFieldId == projectCharacterFieldId && f.ProjectId == projectId);
    }

    public async Task<ProjectCharacterFieldDropdownValue> GetFieldValue(int projectId, int projectCharacterFieldDropdownValueId)
    {
      return await Ctx.Set<ProjectCharacterFieldDropdownValue>()
        .Include(f => f.Project)
        .Include(f => f.ProjectCharacterField)
        .SingleOrDefaultAsync(
          f =>
            f.ProjectId == projectId &&
            f.ProjectCharacterFieldDropdownValueId == projectCharacterFieldDropdownValueId);
    }

    public Task<Project> GetProjectWithFinances(int projectid)
      => Ctx.ProjectsSet.Include(f => f.Claims)
        .Include(f => f.FinanceOperations)
        //.Include(p => p.FinanceOperations.Comments)
        //.Include users
        .SingleOrDefaultAsync(p => p.ProjectId == projectid);
  }

}