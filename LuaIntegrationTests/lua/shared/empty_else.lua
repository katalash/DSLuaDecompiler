function emptyelse1()
    if a() then
        b()
    else
    end
    return
end

function emptyelse2()
    if a() then
        b()
    elseif c() then
        d()
    else
        return
    end
    return
end

function emptyelse3()
    if a() then
        b()
    elseif c() then
    else
        return
    end
    return
end

function emptyelse4()
    if a() then
    elseif c() then
    else
        return
    end
    return
end

function emptyelse5()
    if a() then
        if b() then
            c()
        else
            d()
        end
    else
    end
    return
end

function emptyelse6()
    if a() then
        if b() then
            c()
        else
        end
    else
    end
    return
end

function emptyelse7()
    if a() then
        b()
    else
        if d() then
        end
    end
    return
end

function emptyelse8()
    if a() then
        b()
    else
        if d() then
        else
        end
    end
    return
end

function emptyelse9()
    if a() then
        if b() then
            c()
        end
    else
        if d() then
            f()
        elseif e() then
        end
    end
    return
end 