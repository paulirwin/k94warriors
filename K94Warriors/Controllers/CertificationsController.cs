﻿using K94Warriors.Data.Contracts;
using K94Warriors.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace K94Warriors.Controllers
{
    public class CertificationsController : BaseController
    {
        private readonly IRepository<DogProfile> _dogProfileRepo;
        private readonly IRepository<DogCertification> _dogCertificationRepo;

        public CertificationsController(IRepository<DogProfile> dogProfileRepo, 
            IRepository<DogCertification> dogCertificationRepo)
        {
            _dogProfileRepo = dogProfileRepo;
            _dogCertificationRepo = dogCertificationRepo;
        }

        public ActionResult Index(DogProfile dog)
        {
            var model = _dogCertificationRepo
                .Where(i => i.DogProfileID == dog.ProfileID)
                .OrderBy(i => i.Name);

            SetDogViewBag(dog);

            return View(model);
        }

        [HttpGet]
        public ActionResult Create(DogProfile dog)
        {
            var model = new DogCertification { DogProfileID = dog.ProfileID };

            SetDogViewBag(dog);

            return View(model);
        }

        [HttpPost]
        public ActionResult Create(DogCertification model)
        {
            if (!ModelState.IsValid)
            {
                var dog = _dogProfileRepo.GetById(model.DogProfileID);

                SetDogViewBag(dog);

                return View(model);
            }

            _dogCertificationRepo.Insert(model);

            return RedirectToAction("Index", new { dog = model.DogProfileID });
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var model = _dogCertificationRepo.GetById(id);

            if (model == null)
                return HttpNotFound();

            var dog = model.DogProfile;

            SetDogViewBag(dog);

            return View(model);
        }

        [HttpPost]
        public ActionResult Edit(DogCertification model)
        {
            if (!ModelState.IsValid)
            {
                var dog = _dogProfileRepo.GetById(model.DogProfileID);

                SetDogViewBag(dog);

                return View(model);
            }

            _dogCertificationRepo.Update(model);

            return RedirectToAction("Index", new { dog = model.DogProfileID });
        }

        public ActionResult Delete(int id)
        {
            var model = _dogCertificationRepo.GetById(id);

            if (model == null)
                return HttpNotFound();

            _dogCertificationRepo.Delete(id);

            return RedirectToAction("Index", new { dog = model.DogProfileID });
        }
    }
}
