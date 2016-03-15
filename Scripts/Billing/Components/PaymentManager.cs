﻿//----------------------------------------------
// Flip Web Apps: Game Framework
// Copyright © 2016 Flip Web Apps / Mark Hewitt
//----------------------------------------------
#if UNITY_PURCHASING

using System;
using System.ComponentModel;
using FlipWebApps.GameFramework.Scripts.GameObjects.Components;
using FlipWebApps.GameFramework.Scripts.GameStructure;
using FlipWebApps.GameFramework.Scripts.GameStructure.Characters.ObjectModel;
using FlipWebApps.GameFramework.Scripts.GameStructure.Levels.ObjectModel;
using FlipWebApps.GameFramework.Scripts.GameStructure.Worlds.ObjectModel;
using FlipWebApps.GameFramework.Scripts.Localisation;
using FlipWebApps.GameFramework.Scripts.UI.Dialogs.Components;
using FlipWebApps.GameFramework.Scripts.UI.Other.Components;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Purchasing;

namespace FlipWebApps.GameFramework.Scripts.Billing.Components
{

    /// <summary>
    /// Provides code for setting up and callind in app billing. This derives from IStoreListener to enable it to receive 
    /// messages from Unity Purchasing.
    /// </summary>
    public class PaymentManager : SingletonPersistant<PaymentManager>, IStoreListener
    {
        [Category("Payment Setup")]
        // setup values
        public bool InitOnAwake = true;
        public PaymentProduct[] Products;

        // actions called when some standard products are purchased
        public Action<int> WorldPurchased;
        public Action<int> LevelPurchased;
        public Action<int> CharacterPurchased;
        public Action UnlockGamePurchased;

        // setup references
        private IStoreController _controller;              // Reference to the Purchasing system.
        private IExtensionProvider _extensions;            // Reference to store-specific Purchasing subsystems.

        /// <summary>
        /// Called on startup.
        /// </summary>
        protected override void GameSetup()
        {
            // Initialise purchasing
            if (InitOnAwake)
            {
                InitializePurchasing();
            }
        }


        public void InitializePurchasing()
        {
            // If we have already connected to Purchasing ...
            if (IsInitialized())
            {
                // ... we are done here.
                return;
            }

            // Create a builder, first passing in a suite of Unity provided stores.
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

            // Add products to sell / restore by way of its identifier, associating the general identifier with its store-specific identifiers.
            Assert.IsTrue(Products.Length > 0, "You need to add products if using Payments");
            foreach (PaymentProduct product in Products)
                builder.AddProduct(product.Name, product.ProductType);

            UnityPurchasing.Initialize(this, builder);
        }


        private bool IsInitialized()
        {
            // Only say we are initialized if both the Purchasing references are set.
            return _controller != null && _extensions != null;
        }


        public virtual void BuyProductId(string productId)
        {
            // If the stores throw an unexpected exception, use try..catch to protect my logic here.
            try
            {
                // If Purchasing has been initialized ...
                if (IsInitialized())
                {
                    // ... look up the Product reference with the general product identifier and the Purchasing system's products collection.
                    Product product = _controller.products.WithID(productId);

                    // If the look up found a product for this device's store and that product is ready to be sold ... 
                    if (product != null && product.availableToPurchase)
                    {
                        Debug.Log (string.Format("Purchasing product asychronously: '{0}'", product.definition.id));// ... buy the product. Expect a response either through ProcessPurchase or OnPurchaseFailed asynchronously.
                        _controller.InitiatePurchase(product);
                    }
                    // Otherwise ...
                    else
                    {
                        // ... report the product look-up failure situation  
                        DialogManager.Instance.ShowError(textKey: "Billing.NotAvailable");
                    }
                }
                // Otherwise ...
                else
                {
                    // ... report the fact Purchasing has not succeeded initializing yet. Consider waiting longer or retrying initiailization.
                    DialogManager.Instance.ShowError(textKey: "Billing.NotInitialised");
                }
            }
            // Complete the unexpected exception handling ...
            catch (Exception e)
            {
                // ... by reporting any unexpected exception for later diagnosis.
                DialogManager.Instance.ShowError(LocaliseText.Format("GeneralMessage.Error.GeneralError", e.ToString()));
            }
        }


        // Restore purchases previously made by this customer. Some platforms automatically restore purchases. Apple currently requires explicit purchase restoration for IAP.
        public void RestorePurchases()
        {
            // If Purchasing has been initialised ...
            if (IsInitialized())
            {
                //TODO: the below conditional should not be needed as interfaces should return empty on unsupported platforms!
                // If we are running on an Apple device ... 
                //if (Application.platform == RuntimePlatform.IPhonePlayer || 
                //	Application.platform == RuntimePlatform.OSXPlayer)
                //{
                //	// ... begin restoring purchases
                Debug.Log("RestorePurchases started ...");

                // Fetch the Apple store-specific subsystem.
                var apple = _extensions.GetExtension<IAppleExtensions>();
                // Begin the asynchronous process of restoring purchases. Expect a confirmation response in the Action<bool> below, and ProcessPurchase if there are previously purchased products to restore.
                apple.RestoreTransactions((result) => {
                    // The first phase of restoration. If no more responses are received on ProcessPurchase then no purchases are available to be restored.
                    Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
                    if (result)
                    {
                        // This does not mean anything was restored,
                        // merely that the restoration process succeeded.
                        DialogManager.Instance.ShowInfo(textKey: "Billing.RestoreSucceeded");
                    }
                    else {
                        // Restoration failed.
                        DialogManager.Instance.ShowError(textKey: "Billing.RestoreFailed");
                    }
                });
                //}
                //// Otherwise ...
                //else
                //{
                //	// We are not running on an Apple device. No work is necessary to restore purchases.
                //	Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
                //}
            }
            // Otherwise ...
            else
            {
                // ... report the fact Purchasing has not succeeded initializing yet. Consider waiting longer or retrying initiailization.
                DialogManager.Instance.ShowError(textKey: "Billing.NotInitialised");
            }
        }


        /// <summary>
        /// Called when a purchase completes. This automatically handles certain types of purchase and notifications
        /// TODO Add characher unlock
        /// 
        /// May be called at any time after OnInitialized().
        /// </summary>
        public virtual PurchaseProcessingResult ProcessPurchase(string productId)
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", productId));

            if (string.Equals(productId, "android.test.purchased", StringComparison.Ordinal))
            {
                DialogManager.Instance.ShowInfo("Test payment android.test.purchased purchased ok");
            }

            else if (productId.Equals("unlockgame"))
            {
                // update on GameManager
                GameManager.Instance.IsUnlocked = true;
                PlayerPrefs.SetInt("IsUnlocked", 1);
                PlayerPrefs.Save();

                // notify all subscribers of the purchase
                if (UnlockGamePurchased != null)
                    UnlockGamePurchased();
            }

            else if (productId.StartsWith("unlock.world."))
            {
                int number = int.Parse(productId.Substring("unlock.world.".Length));
                World world = null;

                // first try and get from game manager
                if (GameManager.Instance.Worlds != null)
                    world = GameManager.Instance.Worlds.GetItem(number);

                // if not found on game manager then create a new copy to ensure this purchase is recorded
                if (world == null)
                    world = new World(number);

                // mark the item as bought and unlocked
                world.MarkAsBought();

                // notify all subscribers of the purchase
                if (WorldPurchased != null)
                    WorldPurchased(number);
            }

            else if (productId.StartsWith("unlock.level."))
            {
                int number = int.Parse(productId.Substring("unlock.level.".Length));
                Level level = null;

                // first try and get from game manager
                if (GameManager.Instance.Levels != null)
                    level = GameManager.Instance.Levels.GetItem(number);

                // if not found on game manager then create a new copy to ensure this purchase is recorded
                if (level == null)
                    level = new Level(number);

                // mark the item as bought and unlocked
                level.MarkAsBought();

                // notify all subscribers of the purchase
                if (LevelPurchased != null)
                    LevelPurchased(number);
            }

            else if (productId.StartsWith("unlock.character."))
            {
                int number = int.Parse(productId.Substring("unlock.character.".Length));
                Character character = null;

                // first try and get from game manager
                if (GameManager.Instance.Characters != null)
                    character = GameManager.Instance.Characters.GetItem(number);

                // if not found on game manager then create a new copy to ensure this purchase is recorded
                if (character == null)
                    character = new Character(number);

                // mark the item as bought and unlocked
                character.MarkAsBought();

                // notify all subscribers of the purchase
                if (CharacterPurchased != null)
                    CharacterPurchased(number);
            }
            return PurchaseProcessingResult.Complete;
        }


        //  
        // --- IStoreListener
        //

        /// <summary>
        /// Called when Unity IAP is ready to make purchases.
        /// </summary>
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            // Purchasing has succeeded initializing. Collect our Purchasing references.
            Debug.Log("OnInitialized: PASS");

            // Overall Purchasing system, configured with products for this application.
            this._controller = controller;
            // Store specific subsystem, for accessing device-specific store features.
            this._extensions = extensions;
        }


        /// <summary>
        /// Called when Unity IAP encounters an unrecoverable initialization error.
        ///
        /// Note that this will not be called if Internet is unavailable; Unity IAP
        /// will attempt initialization until it becomes available.
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            // Purchasing set-up has not succeeded. Check error for reason. Consider sharing this reason with the user.
            Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
        }


        /// <summary>
        /// Called when a purchase completes.
        ///
        /// May be called at any time after OnInitialized().
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            return ProcessPurchase(args.purchasedProduct.definition.id);
        }



        /// <summary>
        /// Called when a purchase fails.
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));

            // A product purchase attempt did not succeed. Check failureReason for more detail. Consider sharing this reason with the user.
            switch (failureReason)
            {
                // for these cases we don't need to inform further
                case PurchaseFailureReason.UserCancelled:
                    break;
                // for these we show an error
                default:
                    DialogManager.Instance.ShowError(LocaliseText.Format("GeneralMessage.Error.GeneralError", failureReason));
                    break;
            }
        }


        public override string ToString()
        {
            string result = "";
            if (_controller != null && _controller.products != null)
                foreach (var product in _controller.products.all)
                {
                    result += string.Format("{0}, {1}, {2}\n", product.metadata.localizedTitle, product.metadata.localizedDescription, product.metadata.localizedPriceString);
                }
            return result;
        }
    }

    [Serializable]
    public class PaymentProduct
    {
        public ProductType ProductType;
        public string Name;
    }
}

#endif