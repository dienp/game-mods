local settings = require("settings")

local pageName = "WBP_Gallery_Achievement_Page_C"
local pagePath = "/Game/TPS/UI/Gallery/Achievement/WBP_Gallery_Achievement_Page." .. pageName
local buttonPath = "/Game/TPS/UI/Foundation/Widgets/TPSButton.TPSButton_C"
local stylePath = "/Game/TPS/UI/Styles/Style_Button_Common.Style_Button_Common_C"
local clickPath = "/Script/CommonUI.CommonButtonBase:HandleButtonClicked"
local columnName = "EndlessAliceAchievementUnlockColumn"

local enabled = settings.Enabled ~= false
local batchSize = math.max(1, math.min(20, math.floor(tonumber(settings.BatchSize) or 8)))
local collapsed = 1 -- ESlateVisibility::Collapsed
local alignRight = 3 -- EHorizontalAlignment::HAlign_Right
local fill = 1 -- ESlateSizeRule::Fill

local function valid(object)
    return object ~= nil and object:IsValid()
end

local function log(message)
    print("[AchievementUnlock] " .. message .. "\n")
end

local function findClass(path)
    local class = StaticFindObject(path)
    if not valid(class) then
        LoadAsset((path:gsub("_C$", "")))
        class = StaticFindObject(path)
    end
    return class
end

local function pendingAchievements(system)
    local pending, total = {}, 0
    for _, entry in ipairs(system:GetAchievements()) do
        local achievement = entry:get()
        if valid(achievement) then
            total = total + 1
            if not achievement:IsCompleted() then
                pending[#pending + 1] = achievement
            end
        end
    end
    return pending, total
end

local function start()
    local subsystems = StaticFindObject("/Script/Engine.Default__SubsystemBlueprintLibrary")
    local achievementClass = StaticFindObject("/Script/Demo.TPSAchievementSystem")
    local widgets = StaticFindObject("/Script/UMG.Default__WidgetBlueprintLibrary")
    local borderClass = StaticFindObject("/Script/UMG.Border")
    local columnClass = StaticFindObject("/Script/UMG.VerticalBox")

    for _, dependency in ipairs({ subsystems, achievementClass, widgets, borderClass, columnClass }) do
        if not valid(dependency) then
            log("Required game classes are unavailable.")
            return
        end
    end

    local pages = {}
    local running = false
    local buttonClass, styleClass

    local function getAchievementSystem(page)
        local owner = page:GetOwningPlayer()
        if valid(owner) and owner:IsLocalController() then
            local system = subsystems:GetLocalPlayerSubSystemFromPlayerController(owner, achievementClass)
            if valid(system) and system:IsReady() then
                return system
            end
        end
    end

    local function label(button, text, clickable)
        if valid(button) then
            button:SetButtonText(FText(text))
            button:SetIsInteractionEnabled(clickable)
        end
    end

    local function unlockAll(page, button)
        if running or not valid(page) or not page:IsActivated() or not valid(button) or not button:IsVisible() then
            return
        end
        local system = getAchievementSystem(page)
        if not valid(system) then
            label(button, "Try Again", true)
            log("The local achievement system is not ready.")
            return
        end

        local pending, total = pendingAchievements(system)
        if total == 0 then
            label(button, "Try Again", true)
            log("The local achievement system returned no entries.")
            return
        end
        if #pending == 0 then
            label(button, ("All %d unlocked"):format(total), false)
            return
        end

        running = true
        local index, unlocked = 1, 0
        label(button, "Unlocking...", false)

        local function applyBatch()
            if not valid(system) or not system:IsReady() then
                error("The local achievement system became unavailable.")
            end

            local last = math.min(#pending, index + batchSize - 1)
            for i = index, last do
                local achievement = pending[i]
                if valid(achievement) and not achievement:IsCompleted() then
                    local ok, message = pcall(function()
                        achievement:SetProgress(1.0)
                    end)
                    if ok and achievement:IsCompleted() then
                        unlocked = unlocked + 1
                    elseif not ok then
                        log("Achievement could not be completed: " .. tostring(message))
                    end
                end
            end
            index = last + 1
            if index <= #pending then
                return false
            end

            if unlocked > 0 then
                system:AsyncSave()
            end
            local remaining = pendingAchievements(system)
            running = false
            local completed = total - #remaining
            if #remaining == 0 then
                label(button, ("All %d unlocked"):format(total), false)
            else
                label(button, ("Unlock All (%d left)"):format(#remaining), true)
            end
            if valid(page) and valid(page.Container) then
                page.Container:RegenerateAllEntries()
            end
            log(("Completed %d this time. %d/%d in-game achievements complete."):format(unlocked, completed, total))
            return true
        end

        local function step()
            local ok, completed = pcall(applyBatch)
            if not ok then
                if unlocked > 0 and valid(system) then
                    pcall(function() system:AsyncSave() end)
                end
                running = false
                label(button, "Unlock All", true)
                log("Unlock failed: " .. tostring(completed))
            elseif not completed then
                ExecuteInGameThreadWithDelay(30, step)
            end
        end
        step()
    end

    local function attach(page)
        if not valid(page) or not valid(page.WidgetTree) or not valid(page.Container) then
            return
        end
        local tree = page.WidgetTree
        local root = tree.RootWidget
        if not valid(root) or not root:IsA(borderClass) then
            return
        end
        local child = root:GetContent()
        if not valid(child) then
            return
        end

        local button
        if child:GetFName():ToString() == columnName then
            button = child:GetChildAt(0)
            if not valid(button) then
                return
            end
        elseif enabled then
            local grid = page.Container:GetParent()
            local owner = page:GetOwningPlayer()
            if not valid(grid) or child:GetAddress() ~= grid:GetAddress() or not valid(owner) then
                return
            end

            if not valid(buttonClass) then
                buttonClass = findClass(buttonPath)
            end
            if not valid(styleClass) then
                styleClass = findClass(stylePath)
            end
            if not valid(buttonClass) or not valid(styleClass) then
                return
            end

            local column = StaticConstructObject(columnClass, tree, FName(columnName))
            button = widgets:Create(owner, buttonClass, owner)
            if not valid(column) or not valid(button) then
                return
            end
            button:SetStyle(styleClass)
            button:SetMinDimensions(176, 38)
            button:SetIsFocusable(true)
            button:SetToolTipText(FText("Complete missing achievements for this profile."))
            local action = column:AddChildToVerticalBox(button)
            if not valid(action) then
                return
            end
            action:SetHorizontalAlignment(alignRight)
            action:SetPadding({ Left = 0, Top = 0, Right = 8, Bottom = 8 })

            root:SetContent(column)
            local gridSlot = column:AddChildToVerticalBox(grid)
            if not valid(gridSlot) then
                root:SetContent(grid)
                return
            end
            gridSlot:SetSize({ SizeRule = fill, Value = 1 })
        else
            return
        end

        if not enabled then
            button:SetVisibility(collapsed)
            return
        end

        pages[button:GetAddress()] = page
        button:SetVisibility(0)
        local system = getAchievementSystem(page)
        local pending, total = {}, 0
        if valid(system) then
            pending, total = pendingAchievements(system)
        end
        if total > 0 and #pending == 0 then
            label(button, ("All %d unlocked"):format(total), false)
        else
            label(button, "Unlock All", true)
        end
    end

    local function attachSafely(page)
        local ok, message = pcall(attach, page)
        if not ok then
            log("Could not add the Achievements button: " .. tostring(message))
        end
    end

    if enabled then
        RegisterHook(clickPath, function(context)
            local button = context:get()
            local page = pages[button:GetAddress()]
            if valid(page) then
                ExecuteInGameThread(function()
                    local ok, message = pcall(function()
                        if not valid(page) or not valid(button) then
                            return
                        end
                        local column = page.WidgetTree.RootWidget:GetContent()
                        local attachedButton = valid(column) and column:GetFName():ToString() == columnName
                            and column:GetChildAt(0)
                        if attachedButton and valid(attachedButton) and attachedButton:GetAddress() == button:GetAddress() then
                            unlockAll(page, button)
                        end
                    end)
                    if not ok then
                        running = false
                        label(button, "Unlock All", true)
                        log("Unlock failed: " .. tostring(message))
                    end
                end)
            else
                pages[button:GetAddress()] = nil
            end
        end)
    end

    NotifyOnNewObject(pagePath, function(page)
        ExecuteInGameThread(function()
            attachSafely(page)
        end)
    end)
    for _, page in ipairs(FindAllOf(pageName) or {}) do
        attachSafely(page)
    end
    log(enabled and "Loaded" or "Disabled")
end

ExecuteInGameThread(start)
