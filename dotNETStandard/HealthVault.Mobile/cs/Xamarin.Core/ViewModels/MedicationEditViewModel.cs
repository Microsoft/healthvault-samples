﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using HealthVault.Sample.Xamarin.Core.Services;
using Microsoft.HealthVault.Clients;
using Microsoft.HealthVault.Connection;
using Microsoft.HealthVault.ItemTypes;
using Microsoft.HealthVault.Person;
using Microsoft.HealthVault.Vocabulary;
using Xamarin.Forms;

namespace HealthVault.Sample.Xamarin.Core.ViewModels
{
    public class MedicationEditViewModel : ViewModel
    {
        private readonly Medication medication;
        private readonly IHealthVaultConnection connection;

        public MedicationEditViewModel(
            Medication medication,
            IHealthVaultConnection connection,
            INavigationService navigationService)
            : base(navigationService)
        {
            this.medication = medication;
            this.connection = connection;
            DosageType = medication.Dose?.Display ?? "";
            Strength = medication.Strength?.Display ?? "";
            ReasonForTaking = medication.Indication?.Text ?? "";
            DateStarted = DataTypeFormatter.ApproximateDateTimeToDateTime(medication.DateStarted);
            SaveCommand = new Command(async () => await SaveAsync(medication));
        }

        private IList<VocabularyItem> ingredientChoices;
        public IList<VocabularyItem> IngredientChoices
        {
            get { return this.ingredientChoices; }

            set
            {
                this.ingredientChoices = value;
                this.OnPropertyChanged();
            }
        }

        private VocabularyItem name;
        public VocabularyItem Name
        {
            get { return this.name; }

            set
            {
                this.name = value;
                this.OnPropertyChanged();
            }
        }

        public string DosageType { get; set; }
        public string Strength { get; set; }
        public string TreatmentProvider { get; set; }
        public string ReasonForTaking { get; set; }
        public DateTime DateStarted { get; set; }

        public ICommand SaveCommand { get; }

        public override async Task OnNavigateToAsync()
        {
            await this.LoadAsync(async () =>
            {
                IVocabularyClient vocabClient = this.connection.CreateVocabularyClient();
                var ingredientChoices = new List<VocabularyItem>();

                Vocabulary ingredientVocabulary = null;
                while (ingredientVocabulary == null || ingredientVocabulary.IsTruncated)
                {
                    string lastCodeValue = null;
                    if (ingredientVocabulary != null)
                    {
                        if (ingredientVocabulary.Values.Count > 0)
                        {
                            lastCodeValue = ingredientVocabulary.Values.Last().Value;
                        }
                        else
                        {
                            break;
                        }
                    }

                    ingredientVocabulary = await vocabClient.GetVocabularyAsync(new VocabularyKey("RxNorm Active Ingredients", "RxNorm", "09AB_091102F", lastCodeValue));

                    foreach (string key in ingredientVocabulary.Keys)
                    {
                        ingredientChoices.Add(ingredientVocabulary[key]);
                    }
                }

                this.IngredientChoices = ingredientChoices.OrderBy(c => c.DisplayText).ToList();

                if (medication.Name.Count > 0)
                {
                    Name = this.IngredientChoices.FirstOrDefault(c => c.Value == medication.Name[0]?.Value);
                }

                await base.OnNavigateToAsync();
            });
        }

        private async Task SaveAsync(Medication medication)
        {
            UpdateMedication(medication);

            IThingClient thingClient = this.connection.CreateThingClient();
            PersonInfo personInfo = await this.connection.GetPersonInfoAsync();

            await thingClient.UpdateThingsAsync(personInfo.SelectedRecord.Id, new Collection<Medication>() { medication });

            await NavigationService.NavigateBackAsync();
        }

        private void UpdateMedication(Medication medication)
        {
            if (this.Name != null)
            {
                medication.Name = new CodableValue(this.Name.DisplayText, this.Name);
            }
            bool empty = string.IsNullOrWhiteSpace(DosageType) && medication.Dose == null;
            if (!empty)
            {
                medication.Dose = new GeneralMeasurement(DosageType);
            }

            empty = string.IsNullOrWhiteSpace(Strength) && medication.Strength == null;
            if (!empty)
            {
                medication.Strength = new GeneralMeasurement(Strength);
            }

            empty = string.IsNullOrWhiteSpace(ReasonForTaking) && medication.Indication == null;
            if (!empty)
            {
                medication.Indication = new CodableValue(ReasonForTaking);
            }

            var inputDateEmpty = DateStarted == DataTypeFormatter.EmptyDate;
            empty = inputDateEmpty && medication.DateStarted == null;
            if (!empty)
            {
                medication.DateStarted = new ApproximateDateTime(DateStarted);
            }
        }
    }
}