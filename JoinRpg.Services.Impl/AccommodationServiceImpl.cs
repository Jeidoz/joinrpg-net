using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JoinRpg.Data.Write.Interfaces;
using JoinRpg.DataModel;
using JoinRpg.Domain;
using JoinRpg.Services.Interfaces;
using JoinRpg.Services.Interfaces.Email;
using Microsoft.Practices.ServiceLocation;

namespace JoinRpg.Services.Impl
{
    [UsedImplicitly]
    public class AccommodationServiceImpl : DbServiceImplBase, IAccommodationService
    {
        private IEmailService EmailService { get; }

        public async Task<ProjectAccommodationType> RegisterNewAccommodationTypeAsync(ProjectAccommodationType newAccommodation)
        {
            if (newAccommodation.ProjectId == 0) throw new ActivationException("Inconsistent state. ProjectId can't be 0");
            ProjectAccommodationType result;
            if (newAccommodation.Id != 0)
            {
                result = await UnitOfWork.GetDbSet<ProjectAccommodationType>().FindAsync(newAccommodation.Id).ConfigureAwait(false);
                if (result?.ProjectId != newAccommodation.ProjectId)
                {
                    return null;
                }
                result.Name = newAccommodation.Name;
                result.Cost = newAccommodation.Cost;
                result.Capacity = newAccommodation.Capacity;
                result.Description = newAccommodation.Description;
                result.IsAutoFilledAccommodation = newAccommodation.IsAutoFilledAccommodation;
                result.IsInfinite = newAccommodation.IsInfinite;
                result.IsPlayerSelectable = newAccommodation.IsPlayerSelectable;

            }
            else
            {
                result = UnitOfWork.GetDbSet<ProjectAccommodationType>().Add(newAccommodation);
            }
            await UnitOfWork.SaveChangesAsync().ConfigureAwait(false);
            return result;
        }

        public async Task<IReadOnlyCollection<ProjectAccommodationType>> GetAccommodationForProject(int projectId)
        {
            return await AccomodationRepository.GetAccommodationForProject(projectId).ConfigureAwait(false);
        }

        public async Task<ProjectAccommodationType> GetAccommodationByIdAsync(int accId)
        {
            return await UnitOfWork.GetDbSet<ProjectAccommodationType>().Include(x=>x.ProjectAccommodations)
              .FirstOrDefaultAsync(x => x.Id == accId).ConfigureAwait(false);
        }
        public async Task<ProjectAccommodation> GetProjectAccommodationByIdAsync(int accId)
        {
            return await UnitOfWork.GetDbSet<ProjectAccommodation>()
                .FirstOrDefaultAsync(x => x.Id == accId).ConfigureAwait(false);
        }

        public async Task OccupyRoom(OccupyRequest request)
        {
            var room = await UnitOfWork.GetDbSet<ProjectAccommodation>()
                .Include(r => r.Inhabitants)
                .Include(r => r.ProjectAccommodationType)
                .Where(r => r.ProjectId == request.ProjectId && r.Id == request.RoomId)
                .FirstOrDefaultAsync();

            var accommodationRequest = await UnitOfWork.GetDbSet<AccommodationRequest>()
                .Include(r => r.Subjects.Select(s => s.Player))
                .Include(r => r.Project)
                .Where(r => r.ProjectId == request.ProjectId && r.Id == request.AccommodationRequestId)
                .FirstOrDefaultAsync();

            accommodationRequest.Project.RequestMasterAccess(CurrentUserId, acl => acl.CanSetPlayersAccommodations);

            var freeSpace = room.GetRoomFreeSpace();

            if (freeSpace < accommodationRequest.Subjects.Count)
            {
                throw new JoinRpgInsufficientRoomSpaceException(room);
            }

            accommodationRequest.AccommodationId = room.Id;
            accommodationRequest.Accommodation = room;

            await UnitOfWork.SaveChangesAsync();

            await EmailService.Email(await CreateRoomEmail<OccupyRoomEmail>(accommodationRequest, room));
        }

        private async Task<T> CreateRoomEmail<T>(AccommodationRequest accommodationRequest, ProjectAccommodation room)
        where T: RoomEmailBase, new()
        {
            return new T()
            {
                ChangedRequest = accommodationRequest,
                Initiator = await GetCurrentUser(),
                ProjectName = room.Project.ProjectName,
                Recipients = room.GetSubscriptions().ToList(),
                Room = room,
                Text = new MarkdownString()
            };
        }

        public async Task UnOccupyRoom(UnOccupyRequest request)
        {

            var accommodationRequest = await UnitOfWork.GetDbSet<AccommodationRequest>()
                .Include(r => r.Subjects.Select(s => s.Player))
                .Include(r => r.Project)
                .Where(r => r.ProjectId == request.ProjectId && r.Id == request.AccommodationRequestId)
                .FirstOrDefaultAsync();

            var room = await UnitOfWork.GetDbSet<ProjectAccommodation>()
                .Include(r => r.Inhabitants)
                .Include(r => r.ProjectAccommodationType)
                .Where(r => r.ProjectId == request.ProjectId && r.Id == accommodationRequest.AccommodationId)
                .FirstOrDefaultAsync();

            accommodationRequest.Project.RequestMasterAccess(CurrentUserId, acl => acl.CanSetPlayersAccommodations);

            accommodationRequest.AccommodationId =null;
            accommodationRequest.Accommodation = null;

            await UnitOfWork.SaveChangesAsync();

            await EmailService.Email(await CreateRoomEmail<UnOccupyRoomEmail>(accommodationRequest, room));
        }

        public async Task RemoveAccommodationType(int accomodationTypeId)
        {
            var entity = UnitOfWork.GetDbSet<ProjectAccommodationType>().Find(accomodationTypeId);

            if (entity == null)
            {
                throw new JoinRpgEntityNotFoundException(accomodationTypeId, "ProjectAccommodationType");
            }

            var occupiedRoom =
                entity.ProjectAccommodations.FirstOrDefault(pa => pa.IsOccupied());
            if (occupiedRoom != null)
            {
                throw new RoomIsOccupiedException(occupiedRoom);
            }
            UnitOfWork.GetDbSet<ProjectAccommodationType>().Remove(entity);
            await UnitOfWork.SaveChangesAsync().ConfigureAwait(false);

        }

        public async Task<IEnumerable<ProjectAccommodation>> AddRooms(int projectId, int roomTypeId, string rooms)
        {
            //TODO: Implement rooms names checking

            ProjectAccommodationType roomType = UnitOfWork.GetDbSet<ProjectAccommodationType>().Find(roomTypeId);
            if (roomType == null)
                throw new JoinRpgEntityNotFoundException(roomTypeId, typeof(ProjectAccommodationType).Name);
            if (roomType.ProjectId != projectId)
                throw new ArgumentException($"Room type {roomTypeId} is from another project than specified", nameof(roomTypeId));

            // Internal function
            // Creates new room using name and parameters from given room info
            ProjectAccommodation CreateRoom(string name)
                => new ProjectAccommodation
                {
                    Name = name,
                    AccommodationTypeId = roomTypeId,
                    ProjectId = projectId,
                    ProjectAccommodationType = roomType
                };

            // Internal function
            // Iterates through rooms list and creates object for each room from a list
            IEnumerable<ProjectAccommodation> CreateRooms(string r)
            {
                foreach (string roomCandidate in r.Split(','))
                {
                    int rangePos = roomCandidate.IndexOf('-');
                    if (rangePos > -1)
                    {
                        if (int.TryParse(roomCandidate.Substring(0, rangePos).Trim(), out int roomsRangeStart)
                            && int.TryParse(roomCandidate.Substring(rangePos + 1).Trim(), out int roomsRangeEnd)
                            && roomsRangeStart < roomsRangeEnd)
                        {
                            while (roomsRangeStart <= roomsRangeEnd)
                            {
                                yield return CreateRoom(roomsRangeStart.ToString());
                                roomsRangeStart++;
                            }
                            // Range was defined correctly, we can continue to next item
                            continue;
                        }
                    }

                    yield return CreateRoom(roomCandidate.Trim());
                }
            }

            IEnumerable<ProjectAccommodation> result =
                UnitOfWork.GetDbSet<ProjectAccommodation>().AddRange(CreateRooms(rooms));
            await UnitOfWork.SaveChangesAsync();
            return result;
        }

        private ProjectAccommodation GetRoom(int roomId, int? projectId = null, int? roomTypeId = null)
        {
            var result = UnitOfWork.GetDbSet<ProjectAccommodation>().Find(roomId);

            if (result == null)
                throw new JoinRpgEntityNotFoundException(roomId, typeof(ProjectAccommodation).Name);
            if (projectId.HasValue)
            {
                if (result.ProjectId != projectId.Value)
                    throw new ArgumentException($"Room {roomId} is from different project than specified", nameof(projectId));
            }
            if (roomTypeId.HasValue)
            {
                if (result.AccommodationTypeId != roomTypeId.Value)
                    throw new ArgumentException($"Room {roomId} is from different room type than specified", nameof(projectId));
            }

            return result;
        }

        public async Task EditRoom(int roomId, string name, int? projectId = null, int? roomTypeId = null)
        {
            var entity = GetRoom(roomId, projectId, roomTypeId);
            entity.Name = name;
            await UnitOfWork.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteRoom(int roomId, int? projectId = null, int? roomTypeId = null)
        {
            var entity = GetRoom(roomId, projectId, roomTypeId);
            if (entity.IsOccupied())
            {
                throw new RoomIsOccupiedException(entity);
            }
            UnitOfWork.GetDbSet<ProjectAccommodation>().Remove(entity);
            await UnitOfWork.SaveChangesAsync().ConfigureAwait(false);
        }

        public AccommodationServiceImpl(IUnitOfWork unitOfWork, IEmailService emailService) : base(unitOfWork)
        {
            EmailService = emailService;
        }
    }
}
