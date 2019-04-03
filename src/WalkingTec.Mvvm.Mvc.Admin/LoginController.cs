﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Mvc;
using WalkingTec.Mvvm.Core.Extensions;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WalkingTec.Mvvm.Admin.Api
{
    [ApiController]
    [Route("api/_login")]
    [Public]
    public class LoginController : BaseApiController
    {
        [HttpPost("login")]
        [ActionDescription("登录")]
        public IActionResult Login([FromForm] string userid, [FromForm]string password)
        {
            var user = DC.Set<FrameworkUserBase>()
    .Include(x => x.UserRoles).Include(x => x.UserGroups)
    .Where(x => x.ITCode.ToLower() == userid.ToLower() && x.Password == Utils.GetMD5String(password) && x.IsValid)
    .SingleOrDefault();

            //如果没有找到则输出错误
            if (user == null)
            {
                ModelState.AddModelError(" login", "登录失败");
                return BadRequest(ModelState.GetErrorJson());
            }
            var roleIDs = user.UserRoles.Select(x => x.RoleId).ToList();
            var groupIDs = user.UserGroups.Select(x => x.GroupId).ToList();
            //查找登录用户的数据权限
            var dpris = DC.Set<DataPrivilege>()
                .Where(x => x.UserId == user.ID || (x.GroupId != null && groupIDs.Contains(x.GroupId.Value)))
                .ToList();
            //生成并返回登录用户信息
            LoginUserInfo rv = new LoginUserInfo();
            rv.Id = user.ID;
            rv.ITCode = user.ITCode;
            rv.Name = user.Name;
            rv.Roles = DC.Set<FrameworkRole>().Where(x => user.UserRoles.Select(y => y.RoleId).Contains(x.ID)).ToList();
            rv.Groups = DC.Set<FrameworkGroup>().Where(x => user.UserGroups.Select(y => y.GroupId).Contains(x.ID)).ToList();
            rv.DataPrivileges = dpris;
            rv.PhotoId = user.PhotoId;
            //查找登录用户的页面权限
            var pris = DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .ToList();
            rv.FunctionPrivileges = pris;
            LoginUserInfo = rv;

            LoginUserInfo forapi = new LoginUserInfo();
            forapi.Id = user.ID;
            forapi.ITCode = user.ITCode;
            forapi.Name = user.Name;
            forapi.Roles = rv.Roles;
            forapi.Groups = rv.Groups;
            forapi.PhotoId = rv.PhotoId;
            List<ComboSelectListItem> ms = new List<ComboSelectListItem>();

            var menus = DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .Select(x => x.MenuItem)
                .Where(x => x.MethodName == null)
                .GetSelectListItems(dpris, null, x => x.PageName, x => x.Url);
            ms.AddRange(menus);

            List<string> urls = new List<string>();
            urls.AddRange(DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .Select(x => x.MenuItem)
                .Where(x => x.MethodName != null)
                .Select(x => x.Url)
                );
            urls.AddRange(GlobaInfo.AllModule.Where(x=>x.IsApi == true).SelectMany(x=>x.Actions).Where(x=>(x.IgnorePrivillege == true || x.Module.IgnorePrivillege == true) && x.Url != null).Select(x=>x.Url));
            forapi.Attributes = new Dictionary<string, object>();
            forapi.Attributes.Add("Menus", menus);
            forapi.Attributes.Add("Actions", urls);
            return Ok(forapi);
        }

        [HttpGet("CheckLogin/{id}")]
        public IActionResult CheckLogin(Guid id)
        {
            if(LoginUserInfo?.Id != id)
            {
                return BadRequest();
            }
            else
            {
                return Ok(LoginUserInfo);
            }
        }

        [HttpPost("ChangePassword")]
        public IActionResult ChangePassword(ChangePasswordVM vm)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetErrorJson());
            }
            else
            {
                vm.DoChange();
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState.GetErrorJson());
                }
                else
                {
                    return Ok();
                }
            }

        }

        [HttpGet("Logout/{id}")]
        public ActionResult Logout(Guid id)
        {
            if (LoginUserInfo?.Id == id)
            {
                LoginUserInfo = null;
                HttpContext.Session.Clear();
            }
            return Ok();
        }

    }
}
