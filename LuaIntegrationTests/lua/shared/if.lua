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