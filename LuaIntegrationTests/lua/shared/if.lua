function if_false()
    local a = 6
    c(a)
    if false then
        local b = 6
        d(a, b)
    end

    local e = 9
    f(a, c)
end

function else_if()
    if a() then
        if a() == TRUE then
        elseif b() == TRUE then
        elseif c() == TRUE then
        elseif d() == TRUE then
            e()
        end
    end
    b()
end

function if_compound_or()
    if a() == TRUE and (b() == FALSE or c() == TRUE) then
    elseif (a() == TRUE) then
    end
    c()
    return FALSE
end