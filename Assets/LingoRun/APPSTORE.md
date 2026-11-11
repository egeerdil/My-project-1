# LingoRun — App Store Connect checklist

## 0. One-time prerequisites
- [ ] Apple Developer Program membership ($99/year): https://developer.apple.com/programs/enroll
- [ ] Xcode installed from the Mac App Store (sign in with your Apple ID in
      Xcode > Settings > Accounts)
- [ ] Unity **iOS Build Support** module installed (Unity Hub > Installs >
      your 6000.5.1f1 install > ⚙ > Add modules > iOS Build Support)

## 1. In Unity
1. Menu **Tools > LingoRun > Prepare iOS Build** (sets name, icon, portrait,
   bundle ID, Always Included Shaders).
   - Edit `Assets/LingoRun/Editor/IOSBuildSetup.cs` first if you want a
     different bundle ID than `com.selen.lingorun`. It must be unique on the
     App Store and match step 3.
2. File > Build Profiles > **iOS** > Switch Platform.
3. Add your scene to the build list (SampleScene is fine — the game auto-boots).
4. **Build** → choose an output folder (e.g. `~/Builds/LingoRun-iOS`). Unity
   produces an Xcode project.

## 2. In Xcode
1. Open `Unity-iPhone.xcodeproj` from the build folder.
2. Select the **Unity-iPhone** target > Signing & Capabilities:
   - check **Automatically manage signing**
   - pick your **Team** (appears after Apple ID login; personal team works for
     device testing, but App Store upload needs the paid Developer account team).
3. Plug in an iPhone and press ▶ to test on device first.
4. For the store: select **Any iOS Device (arm64)** as target, then
   **Product > Archive**.
5. When the Organizer opens: **Distribute App > App Store Connect > Upload**.

## 3. In App Store Connect (appstoreconnect.apple.com)
1. **Users? No — go to Apps > + > New App**:
   - Platform iOS, Name "LingoRun" (must be unique storewide — have a backup
     like "LingoRun: Spanish Runner"), language, **Bundle ID** (register it at
     developer.apple.com/account > Identifiers first if it's not in the list),
     SKU (any string, e.g. `lingorun-001`).
2. Your Xcode upload appears under **TestFlight** after ~15 min of processing.
   Test it via the TestFlight app on your phone.
3. Fill the App Store tab:
   - screenshots (6.7" iPhone required — take them in the simulator or device)
   - description, keywords, support URL, marketing URL (optional)
   - **App Privacy**: "Data not collected" (everything is on-device)
   - age rating questionnaire (LingoRun → 4+)
   - category: Education (secondary: Games > Word)
4. Select the build, set pricing (Free), then **Submit for Review**.
   First review typically takes 1–3 days.

## Gotchas
- "No account team" in Xcode → your Developer Program enrollment isn't active yet.
- Magenta objects on device → shaders weren't in Always Included (rerun step 1.1).
- Upload rejected for icon → the icon must have no transparency (ours is opaque RGB, fine).
- Every new upload needs a higher build number (Xcode target > General > Build,
  or `PlayerSettings.iOS.buildNumber` in the setup script).
