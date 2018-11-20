﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WS.Data;
using WS.Interfaces;
using AutoMapper;
using WS.Business.ViewModels;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace WS.Business.Services
{
    public class DocumentService
    {
        private DocumentRepository repo;
        private readonly IMapper mapper;
        private PathProvider pathprovider;
        public DocumentService(IMapper map,DocumentRepository r, PathProvider p)
        {
            mapper = map;
            repo = r;
            pathprovider = p;
        }

        public IEnumerable<DocumentView> GetAll(string id)
        {
            IEnumerable<Document> documents = repo.GetAll(id);
            return mapper.Map<IEnumerable<Document>, IEnumerable<DocumentView>>(documents); 
        }

        public DocumentView Get(int? id)
        {
            Document document = repo.Get(id);
            return mapper.Map<Document, DocumentView>(document);
        }
        public void Create(IFormFile file, string userId, int parentId=0)
        {
            Document doc = new Document { IsFile = true, Size = (int)file.Length, Name = file.FileName,Extention=file.ContentType , UserId=userId, ParentId = parentId, Date_change=DateTime.Now };
            repo.Create(doc);
        }
        public void Create(string folder, string userId, int parentId=0)
        {
            Document doc = new Document { IsFile = false, Size = 0, Name = folder, Extention = "Folder", UserId = userId, ParentId=parentId,Date_change=DateTime.Now };
            repo.Create(doc);
        }
        public void Update(DocumentView documentView)
        {
            Document document = mapper.Map<DocumentView, Document>(documentView);
            repo.Update(document);
        }

        public void Delete(int? id)
        {
            var document = Get(id);
            if (document.IsFile == true)
            {
                repo.Delete(id);
            }
            else
            {
                DeleteFolder(id);
            }
        }

        public void DeleteFolder(int? id)
        {
            var documents=repo.GetAllChildren(id);
            foreach(var doc in documents)
            {
                Delete(doc.Id);
            }
            repo.Delete(id);
        }
        public IEnumerable<DocumentView> GetAllChildren(int? id)
        {
            var documents = repo.GetAllChildren(id);
            return mapper.Map<IEnumerable<Document>, IEnumerable<DocumentView>>(documents);
        }
        public IEnumerable<DocumentView> GetAllRootElements(string userId)
        {
            var documents = repo.GetAllRootElements(userId);
            return mapper.Map<IEnumerable<Document>, IEnumerable<DocumentView>>(documents);
        }
        public int CreateFolders(string folders,string userId, int parentId=0)
        {
            if (folders == null) return 0;
            var folder = folders.Split('/');
            for(int i=0; i < folder.Length - 1; i++)
            {
                Create(folder[i], userId, parentId);
                parentId= repo.GetIdByName(userId,folder[i],parentId);
            }
            return parentId;
        }
        public void UpdateParentId(int id, int parentId)
        {
            var doc = repo.Get(id);
            doc.ParentId = parentId;
            repo.Update(doc);
            if (doc.IsFile == false)
            {
                UpdateFolderParentId(id, parentId);
            }
        }
        public void UpdateFolderParentId(int id, int parentId)
        {
            foreach(var item in repo.GetAllChildren(id))
            {
                UpdateParentId(item.Id, id);
            }
        }
        public void CreateACopy(int id, int parentId)
        {
            var document = repo.Get(id);
            Document doc= new Document { IsFile = document.IsFile, Size = document.Size, Name = document.Name, Extention = document.Extention, UserId = document.UserId, ParentId = parentId, Date_change = DateTime.Now };
            repo.Create(doc);
            var newId = repo.GetIdByName(doc.UserId, doc.Name, parentId);
            var finishPath = GetFilePath(newId);
            finishPath=Path.Combine(pathprovider.GetRootPath(),doc.UserId ,finishPath);
            string startPath = "";
            if (doc.IsFile == false)
            {
                pathprovider.SplitPath(finishPath);
                CreateAFolderCopy(newId, parentId);
            }
            else
            {
                startPath = GetFilePath(id);
                startPath = Path.Combine(pathprovider.GetRootPath(), doc.UserId, startPath);
                File.Copy(startPath, finishPath);
            }

        }
        public void CreateAFolderCopy(int id, int parentId)
        {
            var documents = repo.GetAllChildren(id);
            foreach (var doc in documents)
            {
                CreateACopy(doc.Id, id);
            }
                
        }
        public string GetFilePath(int id)
        {
            string path = "";
            int parentId = id;
            do
            {
                path = Path.Combine(GetParentFolder(ref parentId) , path);
            } while (parentId != 0);
            return path;
        }
        public string GetParentFolder(ref int id)
        {
            var doc=repo.Get(id);
            id = doc.ParentId;
            return doc.Name;
        }
    }
}
