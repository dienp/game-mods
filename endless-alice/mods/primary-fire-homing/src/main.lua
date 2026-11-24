local defaults = require("settings")
local build = require("build_options")
local UEHelpers = require("UEHelpers")
local createTargetMarker = require("target_marker")

local characters = {
    BP_Alice_MainFire_Arrow_C = "Alice",
    BP_Lea_Arrow2_C = "Lea",
    BP_Mia_Bullet_Blast_C = "Mia",
    BP_Mia_Bullet_Blast_Path_C = "Mia",
}

local pawns = {
    BP_Alice_C = "Alice",
    BP_Lea_C = "Lea",
    BP_Mia_C = "Mia",
}

local settings = {}
for _, character in ipairs({ "Alice", "Lea", "Mia" }) do
    local entry = defaults[character] or {}
    local strength = tonumber(entry.Strength) or 100
    settings[character] = entry.Enabled ~= false and math.max(0, math.min(100, strength)) / 100 or 0
end

local range = 5000 -- Shibako's default acquisition distance in Unreal units.
local minimumSightDot = 0.5 -- 60 degrees to either side of the camera aim.
local hostileTeam = 2 -- ETeamAttitude::Hostile
local actorTransform = 1 -- EGameplayAbilityTargetingLocationType::ActorTransform
local fullStrengthTurnRate = 18
local wandPointShotSpeed = 3000 -- InitialSpeed and MaxSpeed on Shibako's projectile.
local rifleMode = 1 -- ENUM_Alice_WeaponMode::NewEnumerator4
local previewInterval = 75

local function valid(object)
    return object ~= nil and object:IsValid()
end

local function log(message)
    print("[PrimaryFireHoming] " .. message .. "\n")
end

local function speedOf(vector)
    return math.sqrt(vector.X ^ 2 + vector.Y ^ 2 + vector.Z ^ 2)
end

local function start()
    local system = StaticFindObject("/Script/Engine.Default__KismetSystemLibrary")
    local gameplay = StaticFindObject("/Script/Engine.Default__GameplayStatics")
    local mathLibrary = StaticFindObject("/Script/Engine.Default__KismetMathLibrary")
    local teams = StaticFindObject("/Script/Demo.Default__TPSBlueprintLibrary")
    local playerClass = StaticFindObject("/Script/Demo.TPSPlayerCharacter")
    local npcClass = StaticFindObject("/Script/Demo.TPSNPCCharacter")
    local projectileClass = StaticFindObject("/Script/Demo.TPSProjectile_Movement")

    for _, dependency in ipairs({ system, gameplay, mathLibrary, teams, playerClass, npcClass, projectileClass }) do
        if not valid(dependency) then
            log("Required game classes are unavailable. Homing is disabled.")
            return
        end
    end

    local updateMarker = createTargetMarker(gameplay, build.Debug)
    local markerFailed = false
    local scopedTag = { TagName = FName("Status.AimDownSight") }

    local function showTarget(owner, target)
        if markerFailed then
            return
        end
        local ok, message = pcall(updateMarker, owner, target)
        if not ok then
            markerFailed = true
            pcall(updateMarker)
            log("Lock-on marker failed: " .. tostring(message))
        end
    end

    local function isSolo(controller, owner)
        if not controller:IsLocalController() then
            return false
        end
        if system:IsStandalone(owner) then
            return true
        end
        if not system:IsServer(owner) or system:IsDedicatedServer(owner) then
            return false
        end
        local gameState = gameplay:GetGameState(owner)
        return valid(gameState) and #gameState.PlayerArray == 1
    end

    local function findTarget(owner, controller)
        local camera = controller.PlayerCameraManager
        if not valid(camera) then
            return
        end
        local origin = camera:GetCameraLocation()
        local forward = mathLibrary:GetForwardVector(camera:GetCameraRotation())
        local candidates = {}
        gameplay:GetAllActorsOfClass(owner, npcClass, candidates)

        local bestTarget
        local bestScore = -math.huge
        local attitude = {}
        for _, candidate in ipairs(candidates) do
            local target = candidate:get()
            if valid(target) then
                local root = target:K2_GetRootComponent()
                if valid(root) then
                    local location = root:K2_GetComponentLocation()
                    local x, y, z = location.X - origin.X, location.Y - origin.Y, location.Z - origin.Z
                    local distance = math.sqrt(x * x + y * y + z * z)
                    if distance > 0 and distance <= range then
                        local sightDot = (x * forward.X + y * forward.Y + z * forward.Z) / distance
                        local score = sightDot - 0.25 * distance / range
                        if sightDot >= minimumSightDot and score > bestScore and target:IsAlive() then
                            teams:SwitchTeamAttitude(owner, target, attitude)
                            if attitude.Result == hostileTeam and controller:LineOfSightTo(target, origin, true) then
                                bestTarget, bestScore = target, score
                            end
                        end
                    end
                end
            end
        end
        return bestTarget
    end

    local function previewTarget()
        local controller = UEHelpers.GetPlayerController()
        if not valid(controller) then
            return
        end
        local owner = controller:K2_GetPawn()
        if not valid(owner) then
            return
        end
        local character = pawns[owner:GetClass():GetFName():ToString()]
        local strength = settings[character]
        if not strength or strength == 0 or not isSolo(controller, owner)
            or not owner:IsAlive() or gameplay:IsGamePaused(owner) then
            return
        end
        if character == "Alice" and owner.CurrentWeaponMode ~= rifleMode then
            return
        end
        if character == "Mia" then
            local abilities = owner.AbilitySystemComponent
            if not valid(abilities) or abilities:GetGameplayTagCount(scopedTag) > 0 then
                return
            end
        end
        return owner, findTarget(owner, controller)
    end

    local warnedPreview = false
    LoopInGameThreadWithDelay(previewInterval, function()
        if markerFailed then
            return
        end
        local ok, owner, target = pcall(previewTarget)
        if ok then
            showTarget(owner, target)
        else
            showTarget()
            if not warnedPreview then
                warnedPreview = true
                log("Lock-on preview failed: " .. tostring(owner))
            end
        end
    end)

    local function applyHoming(projectile, character, strength)
        local owner = projectile:GetOwner()
        if not valid(owner) or not owner:IsA(playerClass) then
            return
        end
        local controller = owner:GetController()
        if not valid(controller) or not isSolo(controller, owner) then
            return
        end

        local movement = projectile.ProjectileMovement
        if not valid(movement) or movement.bIsHomingProjectile then
            return
        end
        local velocity = movement.Velocity
        local speed = speedOf(velocity)
        if speed <= 0 then
            return
        end

        local target = findTarget(owner, controller)
        showTarget(owner, target)
        if not valid(target) then
            return
        end

        -- The game sets the movement component's weak target reference.
        -- false keeps the projectile's original launch direction.
        projectile:SetHomingTargetFromLocationInfo({ SourceActor = target, LocationType = actorTransform }, false)
        if not valid(movement.HomingTargetComponent:get()) then
            return
        end

        if math.abs(speed - wandPointShotSpeed) > 0.01
            or movement.InitialSpeed ~= wandPointShotSpeed or movement.MaxSpeed ~= wandPointShotSpeed then
            local scale = wandPointShotSpeed / speed
            movement.Velocity = { X = velocity.X * scale, Y = velocity.Y * scale, Z = velocity.Z * scale }
            movement.InitialSpeed = wandPointShotSpeed
            movement.MaxSpeed = wandPointShotSpeed
            projectile:ResetProjectileLifeByRange()
        end
        movement.HomingAccelerationMagnitude = wandPointShotSpeed * fullStrengthTurnRate * strength
        movement.bIsHomingProjectile = true
        if build.Debug then
            log(("%s tracking %s at %d%%"):format(character, target:GetFullName(), math.floor(strength * 100 + 0.5)))
        end
    end

    local warned = {}
    RegisterBeginPlayPostHook(function(context)
        local projectile = context:get()
        if not valid(projectile) or not projectile:IsA(projectileClass) then
            return
        end
        local character = characters[projectile:GetClass():GetFName():ToString()]
        local strength = settings[character]
        if not strength or strength == 0 then
            return
        end

        local ok, errorMessage = pcall(applyHoming, projectile, character, strength)
        if not ok and not warned[character] then
            warned[character] = true
            log(character .. " homing failed: " .. tostring(errorMessage))
        end
    end)
    log("Loaded")
end

ExecuteInGameThread(start)
