local asset = "/Game/TPS/Characters/Players/Shibako/BP_Shibako_CrosshairLock.BP_Shibako_CrosshairLock"

local function valid(object)
    return object ~= nil and object:IsValid()
end

return function(gameplay, debugBuild)
    local tag = FName("PrimaryFireHomingTargetMarker")
    local marker
    local markerClass

    if debugBuild then
        -- UE4SS removes the old Lua callbacks on reload. The spawned actors still exist.
        for _, previous in ipairs(FindAllOf("BP_Shibako_CrosshairLock_C") or {}) do
            if valid(previous) and previous:ActorHasTag(tag) then
                previous:CrosshairLock(nil)
                previous:K2_DestroyActor()
            end
        end
    end

    return function(owner, target)
        if valid(marker) and (not valid(target) or marker:GetOwner():GetAddress() ~= owner:GetAddress()) then
            marker:CrosshairLock(nil)
            marker:K2_DestroyActor()
            marker = nil
        end
        if not valid(target) then
            return
        end

        if not valid(marker) then
            if not valid(markerClass) then
                markerClass = StaticFindObject(asset .. "_C")
                if not valid(markerClass) then
                    LoadAsset(asset)
                    markerClass = StaticFindObject(asset .. "_C")
                end
                if not valid(markerClass) then
                    error("Shibako's lock-on marker class is unavailable")
                end
            end

            local transform = {
                Translation = target:K2_GetActorLocation(),
                Rotation = { X = 0, Y = 0, Z = 0, W = 1 },
                Scale3D = { X = 1, Y = 1, Z = 1 },
            }
            local pending = gameplay:BeginDeferredActorSpawnFromClass(owner, markerClass, transform, 1, owner, 0)
            if not valid(pending) then
                return
            end
            pending.Tags = { tag }
            marker = gameplay:FinishSpawningActor(pending, transform, 0)
            if not valid(marker) then
                return
            end
        end

        local previous = marker.LockTarget
        if not valid(previous) or previous:GetAddress() ~= target:GetAddress() then
            marker:CrosshairLock(target)
        end
    end
end
