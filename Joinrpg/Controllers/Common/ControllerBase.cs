﻿using System;
using System.Collections.Generic;
using System.Web.Mvc;
using JoinRpg.DataModel;
using Microsoft.AspNet.Identity;

namespace JoinRpg.Web.Controllers.Common
{
  public class ControllerBase : Controller 
  {
    protected readonly ApplicationUserManager UserManager;

    protected ControllerBase(ApplicationUserManager userManager)
    {
      UserManager = userManager;
    }

    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      ViewBag.IsProduction = filterContext.HttpContext.Request.Url?.Host == "joinrpg.ru";
      base.OnActionExecuting(filterContext);
    }

    protected User GetCurrentUser()
    {
      return UserManager.FindById(CurrentUserId);
    }

    protected int CurrentUserId
    {
      get
      {
        var id = CurrentUserIdOrDefault;
        if (id == null)
          throw new Exception("Authorization required here");
        return id.Value;
      }
    }

    protected int? CurrentUserIdOrDefault
    {
      get
      {
        var userIdString = User.Identity.GetUserId();
        return userIdString == null ? (int?) null : int.Parse(userIdString);
      }
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        UserManager?.Dispose();
      }

      base.Dispose(disposing);
    }

    protected IEnumerable<T> LoadForCurrentUser<T>(Func<int, IEnumerable<T>> loadFunc)
    {
      return User.Identity.IsAuthenticated
        ? loadFunc(CurrentUserId)
        : new T[] {};
    }

    protected IEnumerable<T> LoadForCurrentUser<T>(Func<IEnumerable<T>> loadFunc)
    {
      return User.Identity.IsAuthenticated
        ? loadFunc()
        : new T[] { };
    }

    protected IEnumerable<T> LoadIfMaster<T>(Project project, Func<IEnumerable<T>> load)
    {
      return (User.Identity.IsAuthenticated && project.HasAccess(CurrentUserId))
        ? load()
        : new T[] {};
    }
  }
}