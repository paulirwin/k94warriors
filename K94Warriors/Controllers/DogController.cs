﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ImageResizer;
using K9ImageResizer = K94Warriors.Core.ImageResizing.ImageResizer;
using K94Warriors.Data.Contracts;
using K94Warriors.Models;
using K94Warriors.ViewModels;

namespace K94Warriors.Controllers
{
    [Authorize]
    public class DogController : BaseController
    {
        private readonly IRepository<DogProfile> _dogRepo;
        private readonly IRepository<DogImage> _dogImageRepo;
        private readonly IBlobRepository _blobRepo;

        public DogController(IRepository<DogProfile> dogRepo,
                             IRepository<DogImage> dogImageRepo,
                             IBlobRepository blobRepo)
        {
            if (dogRepo == null)
                throw new ArgumentNullException("dogRepo");
            _dogRepo = dogRepo;

            if (dogImageRepo == null)
                throw new ArgumentNullException("dogImageRepo");
            _dogImageRepo = dogImageRepo;

            if (blobRepo == null)
                throw new ArgumentNullException("blobRepo");
            _blobRepo = blobRepo;
        }


        //
        // GET: /Dog/Index/

        public ActionResult Index()
        {
            var dogs = _dogRepo.GetAll()
                .Include(d => d.Location).ToList();

            return View(dogs);
        }


        // 
        // GET: /Dog/DogProfile/{dogProfileId}

        public ActionResult DogProfile(int id)
        {
            var model = _dogRepo.GetById(id);

            SetDogViewBag(model);

            return View(new DogProfileViewModel(model));
        }


        // 
        // GET: /Dog/Create/

        [HttpGet]
        public ActionResult Create()
        {
            return View(new DogProfile());
        }


        //
        // POST: /Dog/Create/

        [HttpPost]
        public async Task<ActionResult> Create(DogProfile model)
        {
            if (!ModelState.IsValid)
                return View(model);

            //model.CreatedByUserID = CurrentUserId;

            _dogRepo.Insert(model);

            return RedirectToAction("Index");
        }


        //
        // GET: /Dog/Edit/{dogProfileId}

        [HttpGet]
        public ActionResult Edit(int dogProfileId)
        {
            var model = _dogRepo.GetById(dogProfileId);

            if (model == null)
                return RedirectToAction("Index", "Home");

            SetDogViewBag(model);

            return View(model);
        }


        // 
        // POST: /Dog/Edit/

        [HttpPost]
        public async Task<ActionResult> Edit(DogProfile model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _dogRepo.Update(model);

            return RedirectToAction("DogProfile", new { id = model.ProfileID });
        }


        //
        // POST: /Dog/Delete/{dogProfileId}

        [HttpPost]
        public ActionResult Delete(int dogProfileId)
        {
            _dogRepo.Delete(dogProfileId);
            return RedirectToAction("Index");
        }

        #region Partials and Child Actions

        public async Task<ActionResult> DogThumbnail(int dogId, int size)
        {
            var dogImage = _dogImageRepo
                                .GetAll()
                                .FirstOrDefault(i => i.DogProfileID == dogId);

            if (dogImage == null)
            {
                return GetCachedMissingImageThumbnail();
            }

            try
            {
                return await GetSizedImage(dogImage.BlobKey.ToString(), dogImage.MimeType, size, size) ?? GetCachedMissingImageThumbnail();
            }
            catch
            {
                return GetCachedMissingImageThumbnail();
            }
        }

        private ActionResult GetCachedMissingImageThumbnail()
        {
            byte[] cachedImage = HttpContext.Cache["missing_dog_image_thumbnail"] as byte[];

            if (cachedImage == null)
            {
                using (var image = new FileStream(Server.MapPath("/Content/dog_image_missing.gif"), FileMode.Open))
                using (var outStream = new MemoryStream())
                {
                    var settings = new ResizeSettings
                    {
                        Height = 32,
                        Width = 32,
                        Mode = FitMode.Crop,
                        Format = "png"
                    };
                    image.Seek(0, SeekOrigin.Begin);
                    ImageBuilder.Current.Build(image, outStream, settings);
                    var resized = outStream.ToArray();
                    cachedImage = resized;
                    HttpContext.Cache["missing_dog_image_thumbnail"] = cachedImage;
                }
            }
            
            return new FileContentResult(cachedImage, "image/png");
        }

        [ChildActionOnly]
        public ActionResult DogImagesPartial(int id)
        {
            var viewModel = _dogImageRepo.Where(x => x.DogProfileID == id).ToList();
            return PartialView("_DogImagesPartial", viewModel);
        }

        public async Task<ActionResult> ImageForBlobKey(string blobKey, string mimeType, int height = 90, int width = 160)
        {
            return await GetSizedImage(blobKey, mimeType, height, width);
        }

        private async Task<FileContentResult> GetSizedImage(string blobKey, string mimeType, int height, int width)
        {
            try
            {
                var image = await _blobRepo.GetImageAsync<MemoryStream>(blobKey);

                if (image == null) return null;

                using (var outStream = new MemoryStream())
                {
                    var settings = new ResizeSettings
                    {
                        Height = height,
                        Width = width,
                        Mode = FitMode.Crop
                    };
                    image.Seek(0, SeekOrigin.Begin);
                    ImageBuilder.Current.Build(image, outStream, settings);
                    var resized = outStream.ToArray();

                    return new FileContentResult(resized, mimeType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return null;
        }

        public async Task<ActionResult> GetImage(string id)
        {
            var memoryStream = await _blobRepo.GetImageAsync<MemoryStream>(id);
            return File(memoryStream, "image/jpeg");
        }

        [ChildActionOnly]
        public ActionResult GetDogSection(int dogId)
        {
            var dog = _dogRepo.GetById(dogId);
            ViewBag.DogName = dog.Name;
            ViewBag.DogId = dog.ProfileID;

            return View("_DogSection");
        }

        public ActionResult ImageKeysForDogProfile(int dogProfileId)
        {
            var keys = _dogImageRepo
                .Where(image => image.DogProfileID == dogProfileId)
                .Select(image => new
                    {
                        image.BlobKey,
                        image.MimeType
                    });

            return Json(keys, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> UploadDogImages(DogImageUploadViewModel model)
        {
            if (model.Files.Count < 1)
                return Json(new { success = false, message = "No files in request." });

            await UploadFiles(model.DogProfileId, model.Files);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> DeleteImage(string blobKey)
        {
            Guid key;
            var success = Guid.TryParse(blobKey, out key);
            if (!success)
                return Json(new { success = false, message = "Invalid blob key" });

            var image = _dogImageRepo.Where(i => i.BlobKey.Equals(key)).FirstOrDefault();
            if (image == null)
                return Json(new { success = false, message = "No image found for specified key" });

            _dogImageRepo.Delete(image);
            await _blobRepo.DeleteImageIfExistsAsync(blobKey);
            return Json(new { success = true });
        }

        #endregion

        #region Private Methods

        private async Task UploadFiles(int dogProfileId, HttpFileCollectionBase fileCollection)
        {
            var fileList = new List<HttpPostedFileBase>();
            HttpPostedFileBase file;
            var enumerator = fileCollection.GetEnumerator();
            while (enumerator.MoveNext())
                if ((file = enumerator.Current as HttpPostedFileBase) != null)
                    fileList.Add(file);

            await UploadFiles(dogProfileId, fileList);
        }

        private async Task UploadFiles(int dogProfileId, IEnumerable<HttpPostedFileBase> files)
        {
            var dogImages = new List<DogImage>();
            foreach (var file in files)
            {
                var blobKey = Guid.NewGuid();
                var dogImage = new DogImage { BlobKey = blobKey, DogProfileID = dogProfileId, MimeType = file.ContentType };
                dogImages.Add(dogImage);
                await _blobRepo.InsertOrUpdateImageAsync(blobKey.ToString(), file.InputStream);
            }
            _dogImageRepo.Insert(dogImages);
        }

        #endregion
    }
}