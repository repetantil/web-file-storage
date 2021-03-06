﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WS.Interfaces;
using WS.Business.Services;
using WS.Business.ViewModels;
using WS.Data;
using WS.Business;
using Microsoft.AspNetCore.Routing;

namespace WS.Web.Controllers
{
    public class DocumentController : Controller
    {
        private DocumentService _service;
        private PathProvider _pathProvider;
        private readonly UserManager<User> _userManager;


        public DocumentController(PathProvider pathProvider,DocumentService service, UserManager<User> userManager)
        {
            _service = service;
            _pathProvider = pathProvider;
            _userManager = userManager;
          
        }
        [HttpGet]
        public IActionResult Index(int id=0)
        {
            _service.UpdateSharedDocumentList(User);
            string userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");
            _pathProvider.MapId(userId);
            var documents = _service.ConvertToViewModel(_service.GetAllWithotDeleted(userId)); ;
            ViewBag.ParentId = id;
            ViewBag.Type = 0;
            return View(documents);
        }

        public IActionResult ReturnDocumentList(int parentId=0, int type=0)
        {
            if (type == 1) return RedirectToAction("SharedDocuments", new { parentId = parentId });
            string userId = _userManager.GetUserId(User);
            IEnumerable<DocumentViewModel> documents;
            if (parentId != 0) {
                if (_service.IsYourFile(parentId, userId)) documents = _service.ConvertToViewModel(_service.GetAllChildrenWithoutDeleted(parentId));
                else return RedirectToAction("Index");
            } 
            else documents = _service.ConvertToViewModel(_service.GetAllRootElementsWithoutDeleted(userId));
            ViewBag.ParentId = parentId;
            return PartialView("_GetDocuments",documents);
        }

        public IActionResult ReturnDeleted()
        {
            string userId = _userManager.GetUserId(User);
            IEnumerable<DocumentViewModel> documents;
            documents = _service.ConvertToViewModel(_service.GetAllDeletedFiles());
            var documentsSorted = documents.OrderBy(d => d.Document.Date_change);
            string[] pathes = new string[documentsSorted.Count()];
            int i= 0;
            foreach(var doc in documentsSorted)
            {
                pathes[i] = "FoxBox" + _service.GetPathToFile(doc.Document.Id);
                i++;
            }
            ViewBag.Pathes = pathes;
            return PartialView("_GetDeletedDocuments", documentsSorted);
        }
        public IActionResult DeletedFiles(int id = 0)
        {
            string userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");
            _pathProvider.MapId(userId);
            var documents = _service.ConvertToViewModel(_service.GetAllWithotDeleted(userId)); ;
            ViewBag.ParentId = id;
            return View(documents);
        }
        public IActionResult DeletedFileOptions(int id)
        {
            var doc = _service.Get(id);
            return PartialView("_DeletedFileOptions", doc);
        }

        public IActionResult ReturnParent(int id, int type=0)
        {
            var parentId = _service.Get(id).ParentId;
            if (parentId == 0)
            {
                if (type == 1)
                {
                    return RedirectToAction("SharedDocuments", "Document", new { parentId });
                }
                else
                {
                    return RedirectToAction("ReturnDocumentList", "Document", new { parentId });
                }
            }
            return RedirectToAction("ReturnDocumentList", "Document", new { parentId });
        }
            
        public IActionResult FileOptions(int id)
        {
            var doc = _service.Get(id);
            return PartialView("_FileOptions", doc);
        }
        public IActionResult Paste(int id, int parentId, string type)
        {
            if (type == "copy")
            {
                _service.CreateACopy(id, parentId);
            }
            else if (type == "cut")
            {
                _service.UpdateParentId(id,parentId);
            }
            return RedirectToAction("ReturnDocumentList", "Document", new { parentId });
        }
        public IActionResult Rename(int id, string name = "RRr")
        {
            var userId = _userManager.GetUserId(User);
            _service.RenameFile(id, name);
            var parentId = _service.Get(id).ParentId;
            return RedirectToAction("Index", "Document", new { id=parentId });
        }
        [HttpPost]
        public async Task<IActionResult> UploadFiles(IFormFile file, string fullpath,int parentId=0)
        {
            var currentpath = "";
            if (parentId != 0)
            {
                currentpath=_service.GetFilePath(parentId);
            }
            string userId = _userManager.GetUserId(User);
            var folders = _pathProvider.SplitPath(fullpath,_userManager.GetUserId(User),currentpath);
            var uploads = _pathProvider.GetRootPath();
            if (folders != null)
            {
                uploads = Path.Combine(uploads, folders);
                parentId = _service.CreateFolders(fullpath, userId, parentId);
            }
            else
            {
                uploads = Path.Combine(uploads, currentpath);
            }
            if (file.Length > 0)
            {
                using (var fileStream = new FileStream(Path.Combine(uploads, file.FileName), FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                    _service.Create(file,userId,parentId);
                }
            }
            var documents = _service.ConvertToViewModel(_service.GetAll(userId));
            return PartialView("_GetDocuments", documents);
            //return RedirectToAction("Index");
        }
        public IActionResult CreateFolder(int parentId, string name)
        {
            var currentpath = "";
            if (parentId != 0)
            {
                currentpath = _service.GetFilePath(parentId);
            }
            string userId = _userManager.GetUserId(User);
            _pathProvider.CreateFolder(Path.Combine(_pathProvider.GetRootPath(), userId, currentpath, name));
            _service.Create(name, userId, parentId);
            return RedirectToAction("ReturnDocumentList", "Document", new { parentId });
        }
        [HttpPost]
        public JsonResult Delete(int? id)
        {
            bool result = false;
            if (id != null)
            {
                DateTime moveDate = DateTime.Now;
                _service.MoveToTrash(id, moveDate);
                result = true;
            }
            return Json(result);
        }
        public JsonResult FinalDelete(int? id)
        {
            bool result = false;
            if (id != null)
            {
                _service.FirstStepDelete(id);
                result = true;
            }
            return Json(result);
        }

        public JsonResult Restore(int id)
        {
            bool result = true;
            var path = _service.GetFilePath(id);
            var doc = _service.Get(id);
            var docPath = Path.Combine(_pathProvider.GetRootPath(), doc.UserId, path);

            List<DocumentView> repetedDcocs = new List<DocumentView>();

            if (doc.IsFile)
            {
                if (System.IO.File.Exists(docPath))
                {
                    result = false;
                    repetedDcocs.Add(doc);
                }

                else

                    _service.FirstRestore(id);
            }
            else
            {
                if (Directory.Exists(docPath))
                {
                    result = false;
                }
                else
                    _service.FirstRestore(id);
            }
            //    var documents = _service.GetAllDeletedWithIt(id);

            //    foreach (var document in documents)
            //    {
            //        if (document.IsFile)
            //        {
            //            if (System.IO.File.Exists(Path.Combine(_pathProvider.GetRootPath(), document.UserId, _service.GetFilePath(document.Id))))
            //            {
            //                result = false;
            //                repetedDcocs.Add(document);
            //            }
            //        }
            //    }
            //    if (repetedDcocs.Count == 0)
            //        _service.FirstRestore(id);
            //}
            //if (repetedDcocs.Count == 0)
            //    ViewBag.RepetedFiles = null;
            //else
            //    ViewBag.RepetedFiles = repetedDcocs;
            return Json(result);
        }

        public IActionResult ViewFile(int id)
        {
            var doc = _service.Get(id);
            string path = _service.GetFullPath(id);

            if (doc.IsFile)
            {
                string contentType = MimeTypeMap.GetMimeType(doc.Name);
                //return PhysicalFile(path, contentType, doc.Name);
                return File(System.IO.File.OpenRead(path), contentType);
            } 
            return RedirectToAction("Index", doc.ParentId);
        }
        public IActionResult GetSharedDocumentsForUser(int parentId = 0)
        {
            string userId = _userManager.GetUserId(User);
            var documents = _service.GetSharedDocumentsForUser(userId);
            if (parentId != 0) documents = documents.Where(d => d.Document.ParentId == parentId);
            ViewBag.ParentId = 0;
            ViewBag.Type = 1;
            return View("Index", documents);
        }
        public IActionResult SharedDocuments(int parentId = 0)
        {
            string userId = _userManager.GetUserId(User);
            var documents = _service.GetSharedDocumentsForUser(userId);
            documents = documents.Where(d => d.Document.ParentId == parentId);
            ViewBag.ParentId = parentId;
            ViewBag.Type = 1;
            return PartialView("_GetDocuments", documents);
        }
    }
}