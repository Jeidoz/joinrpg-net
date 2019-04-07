using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JoinRpg.Data.Interfaces;
using JoinRpg.DataModel;
using JoinRpg.Domain;
using JoinRpg.Web.Models.CommonTypes;
using JoinRpg.Web.Models.FieldSetup;
using JoinRpg.WebPortal.Managers.Interfaces;

namespace JoinRpg.WebPortal.Managers
{
    /// <summary>
    /// Operations on fields
    /// </summary>
    public class FieldSetupManager
    {
        private ICurrentUserAccessor CurrentUser { get; }
        private IProjectRepository ProjectRepository { get; }
        private ICurrentProjectAccessor CurrentProject { get; }

        /// <summary>
        /// ctor
        /// </summary>
        public FieldSetupManager(
            ICurrentUserAccessor currentUser,
            IProjectRepository project,
            ICurrentProjectAccessor currentProject)
        {
            CurrentUser = currentUser;
            ProjectRepository = project;
            CurrentProject = currentProject;
        }

        /// <summary>
        /// Get all active fields
        /// </summary>
        public async Task<GameFieldListViewModel> GetActiveAsync()
        {
            return await GetFieldsImpl(FieldNavigationPage.ActiveFieldsList, field => field.IsActive);
        }

        /// <summary>
        /// Get all inactive fields
        /// </summary>
        public async Task<GameFieldListViewModel> GetInActiveAsync()
        {
            return await GetFieldsImpl(FieldNavigationPage.DeletedFieldsList, field => !field.IsActive);
        }

        /// <summary>
        /// Get field creation page
        /// </summary>
        public async Task<GameFieldCreateViewModel> CreatePageAsync()
        {
            var project = await ProjectRepository.GetProjectWithFieldsAsync(CurrentProject.ProjectId);
            if (project == null)
            {
                return null;
            }
            return FillFromProject(project, new GameFieldCreateViewModel());
        }

        public async Task<T> FillFailedModel<T>(T model) where T: GameFieldViewModelBase
        {
            var project = await ProjectRepository.GetProjectWithFieldsAsync(CurrentProject.ProjectId);
            if (project == null)
            {
                return null;
            }
            return FillFromProject(project, model);
        }

        /// <summary>
        /// Get field edit page
        /// </summary>
        public async Task<GameFieldEditViewModel> EditPageAsync(int projectFieldId)
        {
            var field = await ProjectRepository.GetProjectField(CurrentProject.ProjectId, projectFieldId);
            if (field == null)
            {
                return null;
            }

            var model = new GameFieldEditViewModel(field, CurrentUser.UserId);
            return FillFromProject(field.Project, model);
        }

        /// <summary>
        /// Page with field settings
        /// </summary>
        public async Task<FieldSettingsViewModel> SettingsPagesAsync()
        {
            var project = await ProjectRepository.GetProjectWithFieldsAsync(CurrentProject.ProjectId);
            if (project == null)
            {
                return null;
            }
            var fields = project.GetOrderedFields();
            return FillFromProject(project, new FieldSettingsViewModel
            {
                NameField = project.Details.CharacterNameField?.ProjectFieldId ?? -1,
                DescriptionField = project.Details.CharacterDescription?.ProjectFieldId ?? -1,
                LegacyModelEnabled = project.Details.CharacterNameLegacyMode,
                PossibleDescriptionFields =
                    ToSelectListItems(
                        fields.Where(f => f.FieldType == ProjectFieldType.Text),
                        "Нет поля с описанием персонажа"
                        ).SetSelected(project.Details.CharacterDescription?.ProjectFieldId),
                PossibleNameFields =
                    ToSelectListItems(
                        fields.Where(f => f.FieldType == ProjectFieldType.String),
                        "Имя персонажа берется из имени игрока"
                        ).SetSelected(project.Details.CharacterNameField?.ProjectFieldId),
            });
        }

        private List<JoinSelectListItem> ToSelectListItems(
            IEnumerable<ProjectField> enumerable,
            string notSelectedName = null
            )
        {
            var list =  enumerable.Select(field => new JoinSelectListItem()
            {
                Value = field.ProjectFieldId,
                Text = field.FieldName,
                
            }).ToList();

            if (notSelectedName != null)
            {

                list.Insert(0, new JoinSelectListItem()
                {
                    Value = -1,
                    Text = notSelectedName,
                });
            }

            return list;
        }

        private async Task<GameFieldListViewModel> GetFieldsImpl(
            FieldNavigationPage page,
            Func<ProjectField, bool> predicate)
        {
            var project = await ProjectRepository.GetProjectWithFieldsAsync(CurrentProject.ProjectId);
            if (project == null)
            {
                return null;
            }
            var fields = project.GetOrderedFields().Where(predicate).ToViewModels(CurrentUser.UserId);
            FieldNavigationModel navigation = GetNavigation(page, project);
            return new GameFieldListViewModel(navigation, fields);
        }

        private FieldNavigationModel GetNavigation(FieldNavigationPage page, Project project)
        {
            return new FieldNavigationModel
            {
                Page = page,
                ProjectId = project.ProjectId,
                CanEditFields = project.HasMasterAccess(CurrentUser.UserId, pa => pa.CanChangeFields)
                    && project.Active,
            };
        }

        private T FillFromProject<T>(
            Project project,
            T viewModel) where T: IFieldNavigationAware
        {
            FieldNavigationPage page;
            if (viewModel is GameFieldCreateViewModel)
            {
                page = FieldNavigationPage.AddField;
            }
            else if (viewModel is GameFieldEditViewModel)
            {
                page = FieldNavigationPage.EditField;
            }
            else if (viewModel is FieldSettingsViewModel)
            {
                page = FieldNavigationPage.FieldSettings;
            }
            else
            {
                page = FieldNavigationPage.Unknown;
            }
            FieldNavigationModel navigation = GetNavigation(page, project);
            viewModel.ProjectId = navigation.ProjectId;
            viewModel.Navigation = navigation;
            return viewModel;
        }
    }
}