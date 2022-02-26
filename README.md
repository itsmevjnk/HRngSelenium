# HRngSelenium
The backend module of HRng that allows for handling operations using Selenium WebDriver.
This module is only supported on desktop platforms due to the requirement of an automatable browser.

## Installation
This module is supposed to be built along with **HRngBackend**.
Build this project using Visual Studio or `dotnet` (refer to the solution's `README.md`).

## Usage
This module by itself is not executable; the backend (**HRngBackend**) and a frontend is needed. See the **LibTests** project for more details.

## Features
As of now, the HRng backend contains resources for:

**Internal (helper) features:**
* Version string parsing (`Versioning.cs`)
* Browser/driver release storage class (`Release.cs`)
* Interface for browser initialization (`IBrowserHelper.cs`)
* macOS `hdiutil` interface (`HDIUtil.cs`)

**Frontend-facing features:**
* Parsing and loading cookies to Selenium (`SeCookies.cs`)
* Facebook post scraping (`FBPost.cs`)
* Facebook credentials-based login (`FBLogin.cs`)
* 7-Zip binary serving (`SevenZip.cs`)
* Google Chrome/Chromium initialization (`ChromeHelper.cs`)
* Mozilla Firefox initialization (`FirefoxHelper.cs`)
* Apple Safari initialization (macOS only) (`SafariHelper.cs`)

These feature(s) are planned to be added:
* Facebook post poll scraping
* Facebook group (both posts groups and chat groups) members list retrieval

## Contributing
Pull requests are welcome.
For major changes, please open an issue first to discuss what you'd like to change.

Any feature change requires **LibTests** to be updated accordingly.