﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using K94Warriors.Data.Contracts;
using K94Warriors.Email;
using K94Warriors.Models;

namespace K94Warriors.ScheduledTaskServices.Tasks
{
    public class MorningEmailTask : IScheduledTask
    {
        private readonly IMailer _mailer;
        private readonly IRepository<DogEvent> _dogEventRepo;
        private readonly IRepository<DogFeedingEntry> _dogFeedingRepo;
        private readonly IRepository<DogMedication> _dogMedicationRepo;

        private readonly IList<string> _to;
        private readonly string _from;
        private readonly string _subject;

        public MorningEmailTask(IMailer mailer, 
                                IRepository<DogEvent> dogEventRepo,
                                IRepository<DogFeedingEntry> dogFeedingRepo,
                                IRepository<DogMedication> dogMedicationRepo,
                                IList<string> to, string from, string subject)
        {
            if (mailer == null)
                throw new ArgumentNullException("mailer");
            _mailer = mailer;
            if (dogEventRepo == null)
                throw new ArgumentNullException("dogEventRepo");
            _dogEventRepo = dogEventRepo;
            if (dogFeedingRepo == null)
                throw new ArgumentNullException("dogFeedingRepo");
            _dogFeedingRepo = dogFeedingRepo;
            if (dogMedicationRepo == null)
                throw new ArgumentNullException("dogMedicationRepo");
            _dogMedicationRepo = dogMedicationRepo;

            if (to == null || !to.Any())
                throw new ArgumentNullException("to");
            _to = to;
            if (string.IsNullOrEmpty(from))
                throw new ArgumentNullException("from");
            _from = from;
            if (string.IsNullOrEmpty("subject"))
                throw new ArgumentNullException("subject");
            _subject = subject;
        }

        public async Task<bool> Run()
        {
            var events = GetEvents(10); // TODO: un-hardcode this
            var feedings = GetFeedings();
            var amFeedings = feedings.Where(f => f.AMFeeding).ToList();
            var pmFeedings = feedings.Where(f => f.PMFeeding).ToList();
            var medications = GetMedications();
            var amMeds = medications.Where(m => m.AMDose).ToList();
            var noonMeds = medications.Where(m => m.NoonDose).ToList();
            var pmMeds = medications.Where(m => m.PMDose).ToList();

            var lists = new Dictionary<string, IList>
                {
                    {"Upcoming Events", events },
                    {"AM Feedings", amFeedings },
                    {"PM Feedings", pmFeedings },
                    {"AM Medications", amMeds },
                    {"Noon Medications", noonMeds },
                    {"PM Medications", pmMeds }
                };

            try
            {
                var eb = new EmailBuilder();

                eb.To(_to)
                    .From(_from)
                    .WithSubject(_subject)
                    .WithBody(lists);

                var email = eb.ToViewModel();

                await _mailer.Send(email);
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }

        private List<DogEvent> GetEvents(int daysInAdvance)
        {
            var date = DateTime.Now.AddDays(daysInAdvance);
            var events = _dogEventRepo
                .Where(e => e.EventDate < date && !e.IsComplete)
                .ToList();
            return events;
        }

        private List<DogFeedingEntry> GetFeedings()
        {
            var feedings = _dogFeedingRepo.GetAll().ToList();
            return feedings;
        }

        private List<DogMedication> GetMedications()
        {
            var medications = _dogMedicationRepo.GetAll().ToList();
            return medications;
        }
    }
}