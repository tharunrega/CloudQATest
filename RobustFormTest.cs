using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace CloudQA.InterviewTask
{
    [TestFixture]
    public class RobustFormTest
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;
        private const string TargetUrl = "https://app.cloudqa.io/home/AutomationPracticeForm";

        [SetUp]
        public void Setup()
        {
            // Initialize Chrome Options (Headless optional but recommended for CI)
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            
            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        }

        [Test]
        public void FillForm_UsingRobustLocators()
        {
            _driver.Navigate().GoToUrl(TargetUrl);
            var fieldsToTest = new[]
            {
                new { Label = "First Name", Value = "CloudQA" },
                new { Label = "Last Name",  Value = "Candidate" },
                new { Label = "Email",      Value = "candidate@example.com" } 
                // Note: You can easily swap these for "Mobile Number" or others present on the form
            };
            foreach (var field in fieldsToTest)
            {
                // Find the input element relative to its label text
                IWebElement inputElement = FindInputByLabelText(field.Label);
                
                // Interact with the element
                ScrollToElement(inputElement);
                inputElement.Clear();
                inputElement.SendKeys(field.Value);

                // Verification (Optional: Validate the value was entered)
                Assert.That(inputElement.GetAttribute("value"), Is.EqualTo(field.Value), 
                    $"Failed to enter text for field labeled '{field.Label}'");
            }
        }

        [TearDown]
        public void Teardown()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }

        /// <summary>
        /// This is the core "Robust" method. It attempts multiple strategies to find an input
        /// associated with a specific label text. It ignores specific IDs or Classes.
        /// </summary>
        private IWebElement FindInputByLabelText(string labelText)
        {
            // Normalize space to ensure " First Name " matches "First Name"
            string xpathLabel = $"//label[contains(normalize-space(), '{labelText}')]";
            
            // Wait for the label to be present first
            try 
            {
                _wait.Until(ExpectedConditions.ElementIsVisible(By.XPath(xpathLabel)));
            }
            catch (WebDriverTimeoutException)
            {
                throw new NotFoundException($"Could not find a visible label containing text: '{labelText}'");
            }

            // Strategy 1: Check 'for' attribute (The most semantic HTML standard)
            // <label for="abc"> -> <input id="abc">
            var labelElement = _driver.FindElement(By.XPath(xpathLabel));
            string forAttribute = labelElement.GetAttribute("for");

            if (!string.IsNullOrEmpty(forAttribute))
            {
                try 
                {
                    return _driver.FindElement(By.Id(forAttribute));
                }
                catch (NoSuchElementException) { /* Continue to next strategy */ }
            }

            // Strategy 2: Proximity - Input is a following sibling or descendant of a shared container
            // This covers cases where <label> and <input> are in the same <div> but don't use 'for'
            // Logic: Find label -> Go up to parent -> Find first 'input' descendant
            try
            {
                return _driver.FindElement(By.XPath($"{xpathLabel}/following::input[1]"));
            }
            catch (NoSuchElementException) { /* Continue */ }

            // Strategy 3: Nested Input
            // <label>Name <input></label>
            try
            {
                return _driver.FindElement(By.XPath($"{xpathLabel}//input"));
            }
            catch (NoSuchElementException) { /* Continue */ }

            // Strategy 4: Fallback to Placeholder or Aria-Label (if label text matches these attributes directly)
            try
            {
                return _driver.FindElement(By.XPath($"//input[@placeholder='{labelText}' or @aria-label='{labelText}']"));
            }
            catch (NoSuchElementException) { /* Continue */ }

            throw new NotFoundException($"Could not locate input field for label: '{labelText}' using any robust strategy.");
        }

        private void ScrollToElement(IWebElement element)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
        }
    }
}